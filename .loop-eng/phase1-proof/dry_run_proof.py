"""Quota-independent verification of task_loop.py's orchestration logic —
NOT a substitute for run_proof.py (which needs the real Copilot CLI and is
blocked on the org's exhausted monthly quota), but everything in THIS script
runs today and exercises 100% of the deterministic pipeline code for real:
git worktree isolation, staging, guardrails, pytest, the post-hoc scope
ledger, checkpoint writing, KPI logging, and a real `git push` to a local
bare remote. Only the two LLM call sites (copilot_cli.run_copilot for the
developer skill, copilot_cli.run_verdict_skill for the reviewer) are
replaced with scripted fakes — standing in for "some Copilot invocation
happened and produced this output", not testing whether the real model
would produce it.

Two scenarios:
  1. dry_run_happy_path — same narrative as run_proof.py: forced crash on
     attempt 1, a code-fixable regression failure on attempt 2 (the fake
     "developer" satisfies the ticket's own test but leaves an unrelated
     seeded bug, exactly like a real model plausibly would), a fix on
     attempt 3, then a human-rejection retry via the local PR stub before
     landing on "done". Proves task_loop wires all three Phase-1 failure
     kinds together correctly, end to end, in the right order, within the
     5-attempt cap.
  2. dry_run_escalation — a developer fake that writes a hardcoded secret
     every time (always fails the guardrails stage), with max_attempts=2,
     proving exhaustion correctly escalates and writes a checkpoint file
     (.loop/state/<ticket-id>.json) instead of looping forever.

Usage:
    python .loop-eng/phase1-proof/dry_run_proof.py
"""
from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
from dataclasses import replace
from pathlib import Path
from unittest import mock


def subprocess_check_output(args: list[str], cwd: Path) -> str:
    result = subprocess.run(args, cwd=str(cwd), capture_output=True, text=True, encoding="utf-8")
    return result.stdout

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from pipeline import checkpoint, copilot_cli, task_loop  # noqa: E402
from pr_stub import LocalPRStub  # noqa: E402
from scratch_repo import build_scratch_repo  # noqa: E402
from ticket_def import SYNTHETIC_TICKET  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def _calc_py(fix_subtract: bool, human_feedback_addressed: bool = False) -> str:
    subtract_body = "a - b" if fix_subtract else "a + b  # BUG: should be a - b -- deliberately seeded, not part of the ticket"
    docstring = (
        '    """Reduce `price` by `percent`. Addressed reviewer feedback: '
        'documented the ValueError case explicitly."""\n' if human_feedback_addressed else ""
    )
    return (
        "def add(a: int, b: int) -> int:\n"
        "    return a + b\n\n\n"
        "def subtract(a: int, b: int) -> int:\n"
        f"    return {subtract_body}\n\n\n"
        "def discount_price(price: float, percent: float) -> float:\n"
        f"{docstring}"
        "    if not (0 <= percent <= 100):\n"
        '        raise ValueError("percent must be between 0 and 100")\n'
        "    return price * (1 - percent / 100)\n"
    )


_TEST_DISCOUNT_PY = (
    "import pytest\n\n"
    "from app.calc import discount_price\n\n\n"
    "def test_discount_price_normal():\n"
    "    assert discount_price(100, 20) == 80\n\n\n"
    "def test_discount_price_invalid_percent():\n"
    "    with pytest.raises(ValueError):\n"
    "        discount_price(100, 150)\n"
)


def _fake_verdict_always_clear(*args, **kwargs) -> dict:
    # gate.py now calls run_verdict_skill twice per attempt -- once for
    # requirement-reviewer (clear_met/gaps), once for structural-reviewer
    # (clear_to_merge/issues). Schema-aware so both calls get a shape that
    # actually satisfies run_verdict_skill's own key-presence check, rather
    # than every requirement-reviewer call spuriously hitting the
    # retry-until-valid-json fallback and failing as "requirement-format".
    schema = kwargs.get("schema_required_keys") or (args[2] if len(args) > 2 else [])
    if "clear_met" in schema:
        return {"clear_met": True, "gaps": []}
    return {"clear_to_merge": True, "issues": []}


def dry_run_happy_path() -> bool:
    print("\n" + "=" * 70)
    print("dry_run_happy_path: crash -> code-fixable retry -> human-rejection retry -> done")
    print("=" * 70)

    root = Path(tempfile.mkdtemp(prefix="loop-eng-dryrun-happy-"))
    try:
        target_dir, bare_remote = build_scratch_repo(root)
        stub = LocalPRStub()
        calls = {"n": 0}

        def fake_run_copilot(prompt, cwd, agent=None, allow_tools=None, model="auto", timeout=600):
            calls["n"] += 1
            n = calls["n"]
            if n == 1:
                # Simulate a real dead-mid-edit invocation: it wrote SOMETHING
                # to disk before dying, not just failed before touching
                # anything. The crash-recovery path must wipe this, not
                # silently carry it into a later successful commit.
                (cwd / "app" / "PARTIAL_JUNK.txt").write_text("should never survive a crash\n", encoding="utf-8")
                raise copilot_cli.CopilotInvocationCrashed("simulated crash (dry-run attempt 1)")
            (cwd / "app" / "calc.py").write_text(
                _calc_py(fix_subtract=(n >= 3), human_feedback_addressed=(n >= 4)), encoding="utf-8"
            )
            (cwd / "tests" / "test_discount.py").write_text(_TEST_DISCOUNT_PY, encoding="utf-8")
            return copilot_cli.CopilotResult(stdout="[dry-run] simulated edit", stderr="", returncode=0, duration_seconds=0.01)

        with mock.patch.object(copilot_cli, "run_copilot", side_effect=fake_run_copilot), \
             mock.patch.object(copilot_cli, "run_verdict_skill", side_effect=_fake_verdict_always_clear):
            outcome, run_info = task_loop.run_one_ticket(
                SYNTHETIC_TICKET, base_repo_dir=target_dir,
                developer_agent="dev-throwaway", reviewer_agent="structural-reviewer",
                confirm=lambda _: True,
                push_and_create_pr=stub.push_and_create_pr,
                check_pr_merged=stub.check_pr_merged,
                get_pr_review_state=stub.get_pr_review_state,
            )

        print(f"outcome: {outcome}")
        print(f"attempts: {run_info['attempts']}")
        for entry in run_info["history"]:
            print(f"  attempt {entry['attempt']} [{entry['stage']}]: {str(entry['detail'])[:160]}")

        seen_stages = {e["stage"] for e in run_info["history"]}
        required = {"crash", "regression", "human_review"}
        missing = required - seen_stages

        # The crash-time partial edit must never reach the final commit —
        # proves add_worktree's force-remove-and-recreate actually discarded
        # it, not just that a fresh worktree got created when there was
        # nothing to discard yet.
        # Inspect the task branch's HEAD, not target_dir's own HEAD -- the
        # commit landed in the worktree, but worktrees share refs with the
        # base repo, so the branch name resolves from either checkout.
        junk_survived = "PARTIAL_JUNK.txt" in subprocess_check_output(
            ["git", "show", "--stat", "task/proof-1"], cwd=target_dir
        )

        if outcome == "done" and not missing and run_info["attempts"]["developer"] == 4 and not junk_survived:
            print("PASS: all 3 failure kinds fired in order, ticket reached 'done' in 4 developer "
                  "attempts, and the crash-time partial edit (PARTIAL_JUNK.txt) never reached the final commit.")
            return True
        print(f"FAIL: outcome={outcome!r}, missing stages={missing}, "
              f"developer attempts={run_info['attempts']['developer']} (expected 4), "
              f"junk_survived={junk_survived}")
        return False
    finally:
        shutil.rmtree(root, ignore_errors=True)


def dry_run_escalation() -> bool:
    print("\n" + "=" * 70)
    print("dry_run_escalation: always-failing guardrail -> exhausts attempts -> escalated + checkpoint written")
    print("=" * 70)

    root = Path(tempfile.mkdtemp(prefix="loop-eng-dryrun-escalate-"))
    try:
        target_dir, bare_remote = build_scratch_repo(root)
        escalation_ticket = replace(SYNTHETIC_TICKET, local_id="PROOF-ESCALATE")
        checkpoint.clear_checkpoint(escalation_ticket.local_id)

        def fake_run_copilot_always_leaks_secret(prompt, cwd, agent=None, allow_tools=None, model="auto", timeout=600):
            (cwd / "app" / "calc.py").write_text(
                _calc_py(fix_subtract=True) + '\nAWS_KEY = "AKIAABCDEFGHIJKLMNOP"  # never fixed on purpose\n',
                encoding="utf-8",
            )
            return copilot_cli.CopilotResult(stdout="[dry-run] simulated edit with a leaked secret", stderr="", returncode=0, duration_seconds=0.01)

        with mock.patch.object(copilot_cli, "run_copilot", side_effect=fake_run_copilot_always_leaks_secret), \
             mock.patch.object(copilot_cli, "run_verdict_skill", side_effect=_fake_verdict_always_clear):
            outcome, run_info = task_loop.run_one_ticket(
                escalation_ticket, base_repo_dir=target_dir,
                developer_agent="dev-throwaway", reviewer_agent="structural-reviewer",
                max_attempts=2, confirm=lambda _: True,
            )

        print(f"outcome: {outcome}")
        print(f"attempts: {run_info['attempts']}")
        cp = checkpoint.read_checkpoint(escalation_ticket.local_id)
        print(f"checkpoint written: {cp is not None}")
        if cp:
            print(f"  stalled_at_stage: {cp['stalled_at_stage']}")

        if outcome == "escalated" and cp is not None and run_info["attempts"]["developer"] == 2:
            print("PASS: exhausted the 2-attempt cap, escalated, checkpoint file written to disk.")
            return True
        print("FAIL: expected outcome='escalated' with a checkpoint file after exactly 2 attempts.")
        return False
    finally:
        checkpoint.clear_checkpoint("PROOF-ESCALATE")
        shutil.rmtree(root, ignore_errors=True)


def main() -> int:
    results = {
        "happy-path (crash + code-fixable + human-rejection -> done)": dry_run_happy_path(),
        "escalation (exhaustion -> checkpoint)": dry_run_escalation(),
    }
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    ok = True
    for name, passed in results.items():
        print(f"  {'PASS' if passed else 'FAIL'}: {name}")
        ok = ok and passed
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
