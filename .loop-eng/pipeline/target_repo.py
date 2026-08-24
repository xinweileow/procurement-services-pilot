"""Git/GitHub operations against the target repo the developer skill writes
real application code into — never this pipeline's own repo. Every git/gh
call runs with an explicit cwd of `target_dir` so this pipeline's own git
state is never touched.

Ported from loopengineering/src/pipeline/target_repo.py (see plan's
Reference implementations section) and generalized: the predecessor's
FastAPI-specific `_scaffold_files`/`ensure_scaffold` are dropped (app
bootstrapping is a Phase 2 concern — pre-flight item 2 — not something a
single hand-written Phase 1 synthetic ticket needs), and every function
takes `target_dir` explicitly rather than defaulting to a single global
TARGET_REPO_PATH, since Phase 1 has no one fixed target repo yet.

New in this restructure (plan's Critical files: "target_repo.py needs PR
review-state polling"): get_pr_review_state.
"""
from __future__ import annotations

import json
import subprocess
from pathlib import Path
from typing import Callable

DEFAULT_BRANCH = "main"


def _run(args: list[str], cwd: Path, check: bool = True) -> subprocess.CompletedProcess:
    result = subprocess.run(
        args, cwd=str(cwd), capture_output=True, text=True, encoding="utf-8", errors="replace"
    )
    if check and result.returncode != 0:
        raise RuntimeError(
            f"Command failed: {' '.join(args)} (cwd={cwd})\nstdout: {result.stdout}\nstderr: {result.stderr}"
        )
    return result


def clone_if_missing(target_dir: Path, url: str) -> Path:
    if (target_dir / ".git").exists():
        return target_dir
    target_dir.parent.mkdir(parents=True, exist_ok=True)
    _run(["git", "clone", url, str(target_dir)], cwd=target_dir.parent)
    return target_dir


def remote_has_branch(branch: str, target_dir: Path) -> bool:
    result = _run(["git", "ls-remote", "--heads", "origin", branch], cwd=target_dir, check=False)
    return bool(result.stdout.strip())


def ensure_main_pushed(confirm: Callable[[str], bool], target_dir: Path) -> bool:
    """The base branch must be pushed once before any PR can be opened.
    Non-fatal if declined: the rest of the loop still runs, PR creation just
    won't succeed until this is done."""
    if not remote_has_branch(DEFAULT_BRANCH, target_dir):
        if not confirm(f"Push initial commit to origin/{DEFAULT_BRANCH}? (target repo has no branches yet)"):
            return False
        _run(["git", "push", "-u", "origin", DEFAULT_BRANCH], cwd=target_dir)
        return True

    _run(["git", "fetch", "origin", DEFAULT_BRANCH], cwd=target_dir)
    ahead = _run(
        ["git", "rev-list", f"origin/{DEFAULT_BRANCH}..{DEFAULT_BRANCH}", "--count"], cwd=target_dir
    ).stdout.strip()
    if ahead != "0":
        if not confirm(f"Local {DEFAULT_BRANCH} is {ahead} commit(s) ahead of origin — push?"):
            return False
        _run(["git", "push", "origin", DEFAULT_BRANCH], cwd=target_dir)
    return True


def sync_main(target_dir: Path) -> None:
    """Fetches and fast-forwards local DEFAULT_BRANCH to match origin's, when
    a remote DEFAULT_BRANCH exists. Without this, every worktree branches off
    a local main that hasn't learned about PRs merged earlier — by this run
    or a human between runs — producing spurious conflicts once a sibling
    ticket's already-merged change isn't present in this one's base.

    Also discards any uncommitted/untracked files left in the *base* checkout
    by a previous escalated ticket — that work isn't lost, it's captured in
    the KPI log and the escalated ticket's own worktree survives separately.
    """
    if remote_has_branch(DEFAULT_BRANCH, target_dir):
        _run(["git", "fetch", "origin", DEFAULT_BRANCH], cwd=target_dir)
        _run(["git", "checkout", DEFAULT_BRANCH], cwd=target_dir)
        _run(["git", "reset", "--hard", f"origin/{DEFAULT_BRANCH}"], cwd=target_dir)
    else:
        _run(["git", "checkout", DEFAULT_BRANCH], cwd=target_dir)
        _run(["git", "reset", "--hard", DEFAULT_BRANCH], cwd=target_dir)
    _run(["git", "clean", "-fd"], cwd=target_dir)


def checkout_task_branch(local_id: str, target_dir: Path) -> str:
    """Syncs local DEFAULT_BRANCH to origin (see sync_main), then branches off
    it for this ticket."""
    branch = f"task/{local_id.lower()}"
    sync_main(target_dir)
    _run(["git", "checkout", "-B", branch], cwd=target_dir)
    return branch


def stage_all(target_dir: Path) -> None:
    _run(["git", "add", "-A"], cwd=target_dir)


def diff_staged(target_dir: Path) -> str:
    return _run(["git", "diff", "--cached"], cwd=target_dir).stdout


def changed_paths_staged(target_dir: Path) -> list[str]:
    result = _run(["git", "diff", "--cached", "--name-only"], cwd=target_dir)
    return [line.strip() for line in result.stdout.splitlines() if line.strip()]


def commit(message: str, target_dir: Path) -> None:
    _run(["git", "commit", "-m", message], cwd=target_dir)


def push_and_create_pr(
    branch: str,
    title: str,
    body: str,
    confirm: Callable[[str], bool],
    target_dir: Path,
) -> str | None:
    """Pauses for explicit confirmation before pushing/opening a PR. Returns
    the PR URL if created, None if the operator declined."""
    prompt = f"Push branch '{branch}' and open a PR against '{DEFAULT_BRANCH}' titled {title!r}?"
    if not confirm(prompt):
        return None

    _run(["git", "push", "-u", "origin", branch], cwd=target_dir)
    result = _run(
        ["gh", "pr", "create", "--title", title, "--body", body, "--head", branch, "--base", DEFAULT_BRANCH],
        cwd=target_dir,
    )
    return result.stdout.strip()


def check_pr_merged(pr_url_or_number: str, target_dir: Path) -> bool:
    result = _run(
        ["gh", "pr", "view", pr_url_or_number, "--json", "state", "-q", ".state"],
        cwd=target_dir, check=False,
    )
    return result.returncode == 0 and result.stdout.strip() == "MERGED"


def get_pr_review_state(pr_url_or_number: str, target_dir: Path) -> str | None:
    """New capability (plan's Critical files: target_repo.py + PR
    review-state polling). Returns GitHub's `reviewDecision` — one of
    "APPROVED", "CHANGES_REQUESTED", "REVIEW_REQUIRED", or None if no
    review has been submitted yet (or the lookup failed). task_loop treats
    "CHANGES_REQUESTED" as its own failure kind: feed the human's comments
    into the diagnosis skill and retry on the same branch, same as a
    code-fixable failure (see Full flow)."""
    result = _run(
        ["gh", "pr", "view", pr_url_or_number, "--json", "reviewDecision", "-q", ".reviewDecision"],
        cwd=target_dir, check=False,
    )
    if result.returncode != 0:
        return None
    value = result.stdout.strip()
    return value or None


def find_pr_for_branch(branch: str, target_dir: Path) -> dict | None:
    """Looks up an existing PR for `branch` without needing to have persisted
    its URL — branch names are deterministic (task/<local_id>). `--state
    all` is required: `gh pr list` defaults to open-only, so a merged PR
    would otherwise never come back."""
    result = _run(
        ["gh", "pr", "list", "--head", branch, "--state", "all", "--json", "url,state", "-q", ".[0]"],
        cwd=target_dir, check=False,
    )
    if result.returncode != 0 or not result.stdout.strip():
        return None
    return json.loads(result.stdout)
