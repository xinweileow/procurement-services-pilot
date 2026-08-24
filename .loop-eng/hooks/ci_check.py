"""Server-side counterpart to the local pre-push hook (pre-flight checklist
item 5: "Pre-push hook is bypassable (--no-verify) — needs a server-side
second line of defense (CI re-check / branch protection), not just the
local hook").

Runs the EXACT SAME check as `pre_push_check.py` — `check_push()` is
imported, not reimplemented, so there is one implementation of "guardrails +
secrets scan over the push range + regression suite" with two trigger
points, not two copies to keep in sync. The only new logic here is
resolving the (local_sha, remote_sha) pair `check_push()` expects from a
GitHub Actions event instead of git's pre-push stdin contract — CI has no
real `git push` invocation to read stdin from.

This script only re-runs the check and exits non-zero on failure; making
that failure actually block a merge is a GitHub branch-protection setting
("Require status checks to pass before merging", pointed at this workflow's
job) applied once per target repo by whoever administers it — not something
this script or its workflow YAML can turn on by itself. Until that setting
is applied, this is a visible CI failure, not an enforced gate — closing
that gap is a manual step, tracked, not silently assumed done.
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pre_push_check import check_push  # noqa: E402

ZERO_SHA = "0" * 40


def _event_shas() -> tuple[str, str]:
    """Resolves (local_sha, remote_sha) — the same pair the local hook reads
    from git's pre-push stdin — from whichever GitHub Actions event
    triggered this run.

    `pull_request`: the PR's head commit (what's about to be merged) against
    its base branch — exactly the diff a reviewer is looking at.
    `push` (e.g. a direct push to main, or after merge): the commit before
    this push against the commit after. Falls back to ZERO_SHA — diffed
    against the empty tree — when GitHub reports no "before" (a new branch's
    first push), matching `pre_push_check.py`'s own new-ref handling.
    """
    event_name = os.environ.get("GITHUB_EVENT_NAME", "")
    if event_name == "pull_request":
        return os.environ["GITHUB_HEAD_SHA"], os.environ["GITHUB_BASE_SHA"]
    return os.environ["GITHUB_SHA"], os.environ.get("GITHUB_EVENT_BEFORE") or ZERO_SHA


def main(repo_root: Path | None = None) -> int:
    if repo_root is None:
        repo_root = Path(
            subprocess.run(
                ["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, encoding="utf-8", check=True,
            ).stdout.strip()
        )
    local_sha, remote_sha = _event_shas()
    stdin_line = f"refs/heads/ci-check {local_sha} refs/heads/ci-check {remote_sha}"
    allowed, message = check_push([stdin_line], repo_root)
    print(message)
    return 0 if allowed else 1


if __name__ == "__main__":
    raise SystemExit(main())
