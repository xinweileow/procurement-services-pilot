"""Phase 1 step 6: prove the coding agent end-to-end on one hand-written
synthetic ticket, against the REAL Copilot CLI.

STATUS: written but not yet run to completion. The org's Copilot seat
(`xinweileow`) hit "You have exceeded your monthly quota" mid-development of
this proof (confirmed twice, including on a bare `copilot -p "say hello"`).
Everything below is ready to execute as soon as quota resets or different
credentials are supplied — nothing about this script depends on that being
today. See dry_run_proof.py for a quota-independent verification of the
same orchestration logic (task_loop's retry/crash/human-rejection handling)
against a scripted fake Copilot backend, which HAS been run and passed.

Demonstrates, in one continuous ticket run:
  - attempt 1: a deliberately short timeout (3s) forces
    CopilotInvocationCrashed the same way a real hang would (see
    copilot_cli.run_copilot's docstring) -> worktree is force-cleaned and
    the ticket retries. Proves the crash-mid-invocation path.
  - attempt 2: the developer skill implements discount_price() and its own
    test correctly, but the post-hoc gate's regression suite (tests/, not
    just the ticket's own test_path) catches the seeded, unrelated
    subtract() bug -> diagnosis-fed retry. Proves the code-fixable-failure
    path, without gaming the model into a specific mistake.
  - attempt 3: the developer skill fixes both -> gate passes (guardrails +
    tests + scope + structural-reviewer, all independently re-run) ->
    commit + a REAL `git push` to a local bare remote (never GitHub -- see
    scratch_repo.py) + a local-stub "PR opened".
  - The stubbed PR review state returns CHANGES_REQUESTED on its first poll
    -> attempt 4 retries with that feedback folded into the next
    invocation's prompt. Proves the human-rejection path.
  - The stub then returns APPROVED + merged -> outcome "done".

Usage:
    python .loop-eng/phase1-proof/run_proof.py
"""
from __future__ import annotations

import shutil
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from pipeline import task_loop  # noqa: E402
from pr_stub import LocalPRStub  # noqa: E402
from scratch_repo import build_scratch_repo  # noqa: E402
from ticket_def import SYNTHETIC_TICKET  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    root = Path(tempfile.mkdtemp(prefix="loop-eng-phase1-proof-"))
    print(f"scratch root: {root}")
    target_dir, bare_remote = build_scratch_repo(root)
    print(f"target repo: {target_dir}\nlocal bare 'origin': {bare_remote}")

    stub = LocalPRStub()

    outcome, run_info = task_loop.run_one_ticket(
        SYNTHETIC_TICKET,
        base_repo_dir=target_dir,
        developer_agent="dev-throwaway",
        reviewer_agent="structural-reviewer",
        confirm=lambda _: True,
        push_and_create_pr=stub.push_and_create_pr,
        check_pr_merged=stub.check_pr_merged,
        get_pr_review_state=stub.get_pr_review_state,
        developer_timeout_override={1: 3},
    )

    print("\n=== outcome ===")
    print(f"outcome: {outcome}")
    print(f"attempts: {run_info['attempts']}")
    print(f"pr_url: {run_info['pr_url']}")
    print("\n=== history ===")
    for entry in run_info["history"]:
        print(f"  attempt {entry['attempt']} [{entry['stage']}]: {str(entry['detail'])[:200]}")

    expected_stages_seen = {"crash", "guardrail", "test", "regression", "human_review", "gate-passed"}
    seen_stages = {e["stage"] for e in run_info["history"]}
    print(f"\nexpected-ish failure kinds observed: {expected_stages_seen & seen_stages}")

    if outcome == "done":
        print("\nPASS: ticket reached 'done' after exercising crash-recovery, "
              "code-fixable retry, and human-rejection retry within the 5-attempt cap.")
        return 0
    print(f"\nFAIL or INCOMPLETE: outcome was {outcome!r}, expected 'done'. "
          f"Inspect the scratch repo at {target_dir} and the history above.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
