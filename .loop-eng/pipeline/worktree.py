"""Git worktree helpers for per-ticket isolation. Each ticket gets its own
worktree so concurrent/retried writes never collide with the base checkout.

Ported verbatim (behavior-for-behavior) from
loopengineering/src/pipeline/worktree.py — plan's Reference implementations
marks this "unchanged, reused as-is". Only the import path changed.
"""
from __future__ import annotations

from pathlib import Path

from .target_repo import DEFAULT_BRANCH, _run


def _is_registered_worktree(wt_path: Path, target_dir: Path) -> bool:
    """True if wt_path is actually registered with target_dir's repo via
    `git worktree add` — not just a directory that happens to exist there
    (e.g. left over from a crashed process, which `git worktree add` would
    otherwise fail on with a confusing conflict)."""
    result = _run(["git", "worktree", "list", "--porcelain"], cwd=target_dir, check=False)
    if result.returncode != 0:
        return False
    resolved = str(wt_path.resolve())
    for line in result.stdout.splitlines():
        if line.startswith("worktree ") and str(Path(line[len("worktree "):]).resolve()) == resolved:
            return True
    return False


def add_worktree(branch: str, target_dir: Path, resume: bool = False) -> Path:
    """Create a git worktree for `branch` (branched off DEFAULT_BRANCH).

    The worktree lives in a sibling directory of `target_dir` so it never
    interferes with the main checkout.

    `resume` — when True and a registered worktree already exists at the
    (deterministic) path for this branch, return it as-is instead of
    force-removing and recreating it, preserving whatever uncommitted files
    a previous escalated attempt left there. Falls through to force-recreate
    when no such worktree exists yet.

    Returns the worktree path.
    """
    safe = branch.replace("/", "-").replace("\\", "-")
    wt_path = target_dir.parent / f"{target_dir.name}-wt-{safe}"

    if resume and wt_path.exists() and _is_registered_worktree(wt_path, target_dir):
        return wt_path

    if wt_path.exists():
        _run(["git", "worktree", "remove", "--force", str(wt_path)], cwd=target_dir, check=False)

    _run(
        ["git", "worktree", "add", "-B", branch, str(wt_path), DEFAULT_BRANCH],
        cwd=target_dir,
    )

    base_venv = target_dir / ".venv"
    wt_venv = wt_path / ".venv"
    if base_venv.exists() and not wt_venv.exists():
        wt_venv.symlink_to(base_venv)

    return wt_path


def remove_worktree(wt_path: Path, target_dir: Path) -> None:
    """Remove a git worktree and prune the stale reference."""
    _run(["git", "worktree", "remove", "--force", str(wt_path)], cwd=target_dir, check=False)
    _run(["git", "worktree", "prune"], cwd=target_dir, check=False)
