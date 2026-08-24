"""Per-ticket background automation loop, plus (Phase 2) the outer loop that
polls Jira and drives it across a whole backlog. Runs one ticket through
developer-skill -> independent post-hoc gate -> commit/PR, retrying on
failure, capped at MAX_LOOP_RETRY_ATTEMPTS (plan: raised from the
predecessor's 3 to 5).

Ported in spirit from loopengineering/src/pipeline/task_loop.py's
run_one_ticket (same attempt-capped retry shape, same
diagnosis-feeds-next-attempt pattern), but retargeted at copilot_cli instead
of the Anthropic API, and extended with all 4 of the plan's Full flow
failure kinds:

- Code-fixable failure (gate rejects the diff) — feed the gate's raw output
  back as feedback, retry. Consumes one attempt.
- Invocation dies mid-way (CopilotInvocationCrashed) — force-clean the
  worktree so the next attempt starts from a clean branch, not from a dead
  invocation's partial edits. Also consumes one attempt.
- Human rejects the PR (get_pr_review_state returns CHANGES_REQUESTED) —
  feed the rejection into the same retry-on-same-branch path as a
  code-fixable failure. Also consumes one attempt.
- Copilot backend unavailable (CopilotUnavailableError) — deferred
  immediately, does NOT consume an attempt, mirrors the predecessor's
  `except (anthropic.APIConnectionError, anthropic.RateLimitError)` handling.
- Jira/GitHub API failure (JiraAPIError, in run_backlog below — Jira calls
  happen in the outer per-ticket loop, not mid-invocation) — never fed to
  diagnosis, never counted against the attempt cap, actionable message
  surfaced to the human (see jira_sync.JiraAPIError).
"""
from __future__ import annotations

import time
from pathlib import Path
from typing import Callable, Literal

from . import checkpoint, copilot_cli, jira_sync, kpi_log, pm_agent, target_repo, task_router, worktree
from .config import DEFAULT_COPILOT_TIMEOUT_SECONDS, MAX_LOOP_RETRY_ATTEMPTS
from .gate import GateResult, TicketLike, run_post_hoc_gate

TicketOutcome = Literal["done", "in_progress", "escalated", "not_started"]

DEVELOPER_ALLOW_TOOLS = ["write", "shell"]


def _default_confirm(_: str) -> bool:
    return True


def _build_prompt(ticket: TicketLike, feedback: str | None) -> str:
    prompt = ticket.description
    if feedback:
        prompt += (
            "\n\n---\nYour previous attempt on this ticket failed. Fix this "
            f"before anything else, then continue:\n{feedback}"
        )
    return prompt


def run_one_ticket(
    ticket: TicketLike,
    base_repo_dir: Path,
    developer_agent: str,
    requirement_reviewer_agent: str = "requirement-reviewer",
    reviewer_agent: str = "structural-reviewer",
    max_attempts: int = MAX_LOOP_RETRY_ATTEMPTS,
    confirm: Callable[[str], bool] = _default_confirm,
    push_and_create_pr: Callable = target_repo.push_and_create_pr,
    check_pr_merged: Callable = target_repo.check_pr_merged,
    get_pr_review_state: Callable = target_repo.get_pr_review_state,
    copilot_timeout: int = DEFAULT_COPILOT_TIMEOUT_SECONDS,
    developer_timeout_override: dict[int, int] | None = None,
    initial_feedback: str | None = None,
    resume: bool = False,
) -> tuple[TicketOutcome, dict]:
    """Runs the full developer-skill -> post-hoc gate -> PR loop for one
    ticket. Returns (outcome, run_info) where run_info carries attempts,
    history, and pr_url for the caller to inspect/log.

    `developer_timeout_override` — {attempt_number: seconds}, lets a caller
    force a specific attempt to use an aggressively short timeout. Only
    exists so Phase 1's proof can deterministically exercise the
    crash-mid-invocation path (CopilotInvocationCrashed) without needing to
    externally race-kill a process — see copilot_cli.run_copilot's docstring
    for why a short timeout and a real hang hit the exact same code path.

    `initial_feedback`/`resume` — together, "starting cold" (both default)
    vs. resuming an escalated ticket (copilot.md's Escalation Flow, option
    B): `initial_feedback` seeds attempt 1's prompt with a diagnosis instead
    of starting blank, and `resume=True` reuses whatever worktree an earlier
    (failed) run already left in place instead of force-recreating it from
    DEFAULT_BRANCH. See `retry_escalated_ticket` below, the only caller that
    passes either.
    """
    # Deliberately NOT target_repo.checkout_task_branch(base_repo_dir) here:
    # that checks the branch out IN the base repo, which then collides with
    # worktree.add_worktree trying to check the same branch out again in an
    # isolated worktree ("'task/x' is already used by worktree at ..."). The
    # two are alternate modes in the predecessor, never combined — worktree
    # mode only needs the base branch synced to origin, then add_worktree
    # branches off DEFAULT_BRANCH directly into the new worktree in one step.
    branch = f"task/{ticket.local_id.lower()}"
    target_repo.sync_main(base_repo_dir)
    worktree_path = worktree.add_worktree(branch, base_repo_dir, resume=resume)

    feedback: str | None = initial_feedback
    attempts = {
        "developer": 0, "guardrail": 0, "test": 0, "regression": 0,
        "scope": 0, "requirement": 0, "review": 0, "crash": 0, "human_review": 0,
    }
    history: list[dict] = []
    start = time.monotonic()

    for attempt in range(1, max_attempts + 1):
        prompt = _build_prompt(ticket, feedback)
        timeout = (developer_timeout_override or {}).get(attempt, copilot_timeout)

        attempts["developer"] += 1
        try:
            copilot_cli.run_copilot(
                prompt, cwd=worktree_path, agent=developer_agent,
                allow_tools=DEVELOPER_ALLOW_TOOLS, timeout=timeout,
            )
        except copilot_cli.CopilotUnavailableError as e:
            history.append({"attempt": attempt, "stage": "developer-unavailable", "detail": str(e)})
            kpi_log.log_task_outcome(
                ticket.local_id, "not_started", attempts, notes=str(e),
                duration_seconds=time.monotonic() - start,
            )
            return "not_started", {"attempts": attempts, "history": history, "pr_url": None}
        except copilot_cli.CopilotInvocationCrashed as e:
            attempts["crash"] += 1
            history.append({"attempt": attempt, "stage": "crash", "detail": str(e)})
            print(f"[{ticket.local_id}] invocation crashed on attempt {attempt}/{max_attempts} — "
                  f"force-cleaning worktree and retrying: {e}")
            worktree_path = worktree.add_worktree(branch, base_repo_dir)
            feedback = f"Your previous attempt did not complete cleanly ({e}). Start fresh."
            continue

        target_repo.stage_all(worktree_path)
        gate: GateResult = run_post_hoc_gate(
            ticket, worktree_path,
            requirement_reviewer_agent=requirement_reviewer_agent, reviewer_agent=reviewer_agent,
        )
        if gate.stage:
            attempts[gate.stage.split("-")[0]] = attempts.get(gate.stage.split("-")[0], 0) + 1
        history.append({"attempt": attempt, "stage": gate.stage or "gate-passed", "detail": gate.output})

        if not gate.passed:
            print(f"[{ticket.local_id}] gate failed at '{gate.stage}' (attempt {attempt}/{max_attempts})")
            feedback = f"[{gate.stage}] {gate.output}"
            continue

        target_repo.commit(f"{ticket.local_id}: {ticket.summary}", worktree_path)
        pr_url = push_and_create_pr(
            branch, f"{ticket.local_id}: {ticket.summary}",
            f"Automated PR for {ticket.local_id}. Structural review: clear_to_merge.",
            confirm=confirm, target_dir=worktree_path,
        )

        if not pr_url:
            print(f"[{ticket.local_id}] PR creation declined — commit left local on branch '{branch}'.")
            kpi_log.log_task_outcome(
                ticket.local_id, "in_progress", attempts, notes="PR declined",
                duration_seconds=time.monotonic() - start,
            )
            return "in_progress", {"attempts": attempts, "history": history, "pr_url": None}

        review_state = get_pr_review_state(pr_url, worktree_path)
        if review_state == "CHANGES_REQUESTED":
            attempts["human_review"] += 1
            history.append({"attempt": attempt, "stage": "human_review", "detail": pr_url})
            print(f"[{ticket.local_id}] human requested changes on {pr_url} "
                  f"(attempt {attempt}/{max_attempts}) — retrying on the same branch")
            feedback = "A human reviewer requested changes on the PR for this ticket. Address their feedback."
            continue

        merged = check_pr_merged(pr_url, worktree_path)
        outcome: TicketOutcome = "done" if merged else "in_progress"
        kpi_log.log_task_outcome(
            ticket.local_id, outcome, attempts, pr_url=pr_url,
            duration_seconds=time.monotonic() - start,
        )
        print(f"[{ticket.local_id}] PR: {pr_url} ({'MERGED' if merged else 'open, not yet merged'})")
        return outcome, {"attempts": attempts, "history": history, "pr_url": pr_url}

    print(f"[{ticket.local_id}] exceeded {max_attempts} attempts — escalating.")
    checkpoint.write_checkpoint(ticket, history[-1]["stage"] if history else "developer", attempts, history, feedback)
    kpi_log.log_task_outcome(
        ticket.local_id, "escalated", attempts, notes=feedback,
        duration_seconds=time.monotonic() - start,
    )
    return "escalated", {"attempts": attempts, "history": history, "pr_url": None}


def run_backlog(
    base_repo_dir: Path,
    jql: str = jira_sync.DEFAULT_JQL,
    limit: int | None = None,
    confirm: Callable[[str], bool] = _default_confirm,
    push_and_create_pr: Callable = target_repo.push_and_create_pr,
    check_pr_merged: Callable = target_repo.check_pr_merged,
    get_pr_review_state: Callable = target_repo.get_pr_review_state,
) -> dict[str, TicketOutcome]:
    """Phase 2 step 4: polls Jira, picks the next eligible ticket (pm_agent,
    no LLM), routes it to a developer-*.md skill by declared stack
    (task_router, no LLM), and drives it through run_one_ticket. Repeats
    until no eligible ticket remains or `limit` is hit.

    `push_and_create_pr`/`check_pr_merged`/`get_pr_review_state` default to
    the real `gh`-backed target_repo.py functions (Phase 2's real usage
    against an actual GitHub-hosted target repo); pass stubs for a scratch
    repo with a local bare remote (no GitHub involved), same pattern
    phase1-proof/pr_stub.py already established.

    Every Jira call (the initial fetch, and mark_ticket_done/escalated after
    each ticket) is wrapped separately: a JiraAPIError here is NEVER a
    reason to diagnose-and-retry or escalate a ticket — a code fix can't fix
    an expired token — so it's caught, its actionable `.detail` is printed,
    and this function returns what it has so far rather than looping
    forever against a broken credential. The ticket itself is left exactly
    as run_one_ticket last set it (e.g. still 'done' even if the Jira
    Done-transition call itself failed) — the human's actionable message
    tells them to re-run once fixed, not to redo the ticket.
    """
    try:
        backlog = jira_sync.fetch_tickets_from_jira(jql)
    except jira_sync.JiraAPIError as e:
        print(e.detail)
        return {}

    outcomes: dict[str, TicketOutcome] = {}
    processed = 0
    while limit is None or processed < limit:
        ticket = pm_agent.pick_next_task(backlog)
        if ticket is None:
            print("=== No eligible tickets (none approved + not_started with satisfied dependencies). ===")
            break

        profile = task_router.classify(ticket)
        developer_agent = task_router.select_developer_skill(ticket)
        print(f"\n=== picked {ticket.local_id} [{profile.tier}, {developer_agent}]: {ticket.summary} ===")
        ticket.loop_status = "in_progress"

        outcome, _run_info = run_one_ticket(
            ticket, base_repo_dir, developer_agent=developer_agent, confirm=confirm,
            push_and_create_pr=push_and_create_pr, check_pr_merged=check_pr_merged,
            get_pr_review_state=get_pr_review_state,
        )
        ticket.loop_status = outcome
        outcomes[ticket.local_id] = outcome
        processed += 1

        try:
            if outcome == "done":
                print(f"[{ticket.local_id}] merged — marking Done in Jira (and its parent, if fully done)...")
                jira_sync.mark_ticket_done(ticket.local_id)
            elif outcome == "escalated":
                print(f"[{ticket.local_id}] escalated — labeling loop-escalated in Jira...")
                jira_sync.mark_ticket_escalated(ticket.local_id)
        except jira_sync.JiraAPIError as e:
            print(e.detail)
            print(f"[{ticket.local_id}] outcome was {outcome!r} but the Jira sync call above failed — "
                  f"Jira's status may now be stale for this ticket until the credential issue is fixed and this is re-run.")

    print(f"\n=== run_backlog finished. {processed} ticket(s) processed. ===")
    return outcomes


def retry_escalated_ticket(
    ticket_id: str,
    base_repo_dir: Path,
    confirm: Callable[[str], bool] = _default_confirm,
    push_and_create_pr: Callable = target_repo.push_and_create_pr,
    check_pr_merged: Callable = target_repo.check_pr_merged,
    get_pr_review_state: Callable = target_repo.get_pr_review_state,
) -> TicketOutcome:
    """copilot.md's Escalation Flow, option B ("One more automated retry —
    retry with the diagnosis feedback, don't take over yet") — the
    `--retry-escalated <ticket-id>` path referenced there and in
    `pm_agent.pick_next_task`'s own skip message.

    Resumes exactly ONE escalated ticket for exactly one more attempt
    (`max_attempts=1` — this is "one more retry", not a fresh 5-attempt
    cycle; that cap already ran out once for this ticket), seeded with its
    checkpoint's last diagnosis and its own worktree left as-is from the
    failed run (`initial_feedback`/`resume=True` on `run_one_ticket` — see
    its docstring). Returns `"not_started"` without touching Jira if no
    checkpoint exists for this ticket (nothing to resume from).

    On success (`"done"`): marks the ticket Done in Jira, clears the
    `loop-escalated` label, and clears the checkpoint — copilot.md's rule
    that a checkpoint is only ever cleared once a ticket actually reaches
    `done`. On any other outcome: Jira and the checkpoint are left exactly
    as `run_one_ticket` itself left them (it already rewrites the checkpoint
    with a fresh diagnosis if this attempt also exhausts), so a human
    choosing option B again, or falling through to option A, sees the
    latest context either way.
    """
    checkpoint_data = checkpoint.read_checkpoint(ticket_id)
    if checkpoint_data is None:
        print(f"[{ticket_id}] no checkpoint found — was this ticket actually escalated?")
        return "not_started"

    try:
        ticket = jira_sync.fetch_ticket(ticket_id)
    except jira_sync.JiraAPIError as e:
        print(e.detail)
        return "escalated"

    developer_agent = task_router.select_developer_skill(ticket)
    print(f"[{ticket_id}] retrying from checkpoint (stalled at '{checkpoint_data['stalled_at_stage']}') via {developer_agent}")

    outcome, _run_info = run_one_ticket(
        ticket, base_repo_dir, developer_agent=developer_agent, max_attempts=1,
        initial_feedback=checkpoint_data.get("last_feedback"), resume=True,
        confirm=confirm, push_and_create_pr=push_and_create_pr,
        check_pr_merged=check_pr_merged, get_pr_review_state=get_pr_review_state,
    )

    if outcome == "done":
        try:
            jira_sync.mark_ticket_done(ticket_id)
            jira_sync.unmark_ticket_escalated(ticket_id)
        except jira_sync.JiraAPIError as e:
            print(e.detail)
            print(f"[{ticket_id}] retry succeeded but the Jira sync call above failed — "
                  f"Jira may still show it as escalated until the credential issue is fixed and this is re-run.")
            return outcome
        checkpoint.clear_checkpoint(ticket_id)
        print(f"[{ticket_id}] retry succeeded — Jira updated, checkpoint cleared.")
    else:
        print(f"[{ticket_id}] retry outcome: {outcome!r} — checkpoint and loop-escalated label left in place.")

    return outcome


def _cli_confirm(prompt: str) -> bool:
    answer = input(f"\n{prompt} [y/N]: ").strip().lower()
    return answer == "y"


def main(argv: list[str] | None = None) -> int:
    import argparse
    import sys

    if sys.platform == "win32":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description="Poll Jira and run the coding-agent loop against the approved backlog.")
    parser.add_argument("target_dir", type=Path, help="Path to the target repo (already cloned, .loop-eng vendored in).")
    parser.add_argument("--jql", type=str, default=jira_sync.DEFAULT_JQL, help="JQL to select tickets (default: the plan's SCRUM restructured-subtasks query).")
    parser.add_argument("--limit", type=int, default=None, help="Max number of tickets to process this run.")
    parser.add_argument("--auto-approve-prs", action="store_true", help="Skip the human PR-approval prompt (pushes + opens PRs unattended).")
    args = parser.parse_args(argv)

    confirm = (lambda _: True) if args.auto_approve_prs else _cli_confirm
    if args.auto_approve_prs:
        print("WARNING: --auto-approve-prs is set — PRs will be pushed and opened without human confirmation.")

    run_backlog(args.target_dir, jql=args.jql, limit=args.limit, confirm=confirm)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
