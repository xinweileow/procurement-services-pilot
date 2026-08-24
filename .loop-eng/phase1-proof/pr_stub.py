"""Local stand-in for the `gh`-backed PR functions in target_repo.py, used
only by Phase 1's synthetic-ticket proof. The scratch repo's remote is a
local bare repo, not GitHub, so `gh pr create`/`gh pr view` have nothing real
to talk to — task_loop.run_one_ticket takes push_and_create_pr /
check_pr_merged / get_pr_review_state as injected callables specifically so
this proof can swap in a scripted local state machine here without changing
a single line of the real orchestration logic. Phase 2, against the real
target repo, injects nothing and gets the real target_repo.py functions by
default.

The scripted sequence: PR review starts REVIEW_REQUIRED, flips to
CHANGES_REQUESTED on the first poll after creation (simulating a human's one
round of feedback), then APPROVED from the second poll onward — proving
task_loop's human-rejection retry path fires for real and then recovers.
"""
from __future__ import annotations

from pathlib import Path
from typing import Callable


class LocalPRStub:
    def __init__(self, review_sequence: list[str] | None = None):
        self.prs: dict[str, dict] = {}
        self._next_id = 1
        # Popped one at a time per PR on each get_pr_review_state call; the
        # last value repeats once exhausted.
        self.review_sequence = review_sequence or ["CHANGES_REQUESTED", "APPROVED"]
        self._review_calls: dict[str, int] = {}

    def push_and_create_pr(
        self, branch: str, title: str, body: str, confirm: Callable[[str], bool], target_dir: Path
    ) -> str | None:
        if not confirm(f"[stub] push '{branch}' and open a local PR titled {title!r}?"):
            return None
        import subprocess
        subprocess.run(["git", "push", "-u", "origin", branch], cwd=str(target_dir), check=True,
                        capture_output=True, text=True, encoding="utf-8")
        pr_url = self.prs.get(branch, {}).get("url")
        if pr_url is None:
            pr_url = f"local-stub-pr://{self._next_id}"
            self._next_id += 1
        self.prs[branch] = {"url": pr_url, "state": "OPEN", "title": title, "body": body}
        return pr_url

    def get_pr_review_state(self, pr_url: str, target_dir: Path) -> str | None:
        n = self._review_calls.get(pr_url, 0)
        self._review_calls[pr_url] = n + 1
        idx = min(n, len(self.review_sequence) - 1)
        return self.review_sequence[idx]

    def check_pr_merged(self, pr_url: str, target_dir: Path) -> bool:
        for pr in self.prs.values():
            if pr["url"] == pr_url:
                return self._review_calls.get(pr_url, 0) >= len(self.review_sequence)
        return False
