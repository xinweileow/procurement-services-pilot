"""Shared scaffold for Phase 1's synthetic-ticket proof: a real, disposable
git repo with a real *local* bare remote as 'origin' (a second directory on
disk, not GitHub) so `git push`/PR mechanics genuinely exercise, without
ever touching the user's real GitHub account — pushing code and opening PRs
is something this harness must never do without asking first, and there's no
need to ask when a local bare repo does the same job for proving the
pipeline's git plumbing.

The scaffold seeds TWO bugs on purpose, at different layers, so the proof
narrative (run_proof.py / dry_run_proof.py) can exercise a real,
non-gamed pipeline-level retry:
  - `subtract()` has a wrong sign. Deliberately NOT something the ticket
    mentions — only the post-hoc gate's full regression suite (tests/,
    not just the ticket's own task_path test) catches it. This is what
    forces a genuine multi-invocation retry: attempt 1 can satisfy the
    ticket's own acceptance test while still failing the independent
    regression re-check, exactly the gap the post-hoc gate exists to close.
"""
from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

TEMPLATES_DIR = Path(__file__).resolve().parents[1] / "agent-templates"


def _run(args: list[str], cwd: Path, check: bool = True) -> subprocess.CompletedProcess:
    result = subprocess.run(
        ["git", *args], cwd=str(cwd), capture_output=True, text=True, encoding="utf-8", check=False
    )
    if check and result.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed (cwd={cwd}):\n{result.stdout}\n{result.stderr}")
    return result


APP_CALC_PY = (
    "def add(a: int, b: int) -> int:\n"
    "    return a + b\n\n\n"
    "def subtract(a: int, b: int) -> int:\n"
    "    return a + b  # BUG: should be a - b -- deliberately seeded, not part of the ticket\n"
)

TEST_CALC_PY = (
    "from app.calc import add, subtract\n\n\n"
    "def test_add():\n"
    "    assert add(2, 3) == 5\n\n\n"
    "def test_subtract():\n"
    "    assert subtract(5, 3) == 2\n"
)

REQUIREMENTS_TXT = "pytest>=8.0.0\n"

README_MD = (
    "# Phase 1 synthetic-ticket proof scaffold\n\n"
    "Minimal Python/pytest app used only to prove the Phase 1 coding-agent "
    "loop end-to-end. Not a real project.\n"
)


def build_scratch_repo(root: Path) -> tuple[Path, Path]:
    """root: an empty directory to build inside. Returns (target_dir,
    bare_remote_dir)."""
    root.mkdir(parents=True, exist_ok=True)
    bare = root / "origin.git"
    target = root / "target-repo"

    _run(["init", "--bare", "-q", str(bare)], cwd=root)
    target.mkdir()
    _run(["init", "-q", "."], cwd=target)
    _run(["config", "user.email", "loop-eng-proof@local"], cwd=target)
    _run(["config", "user.name", "loop-eng-proof"], cwd=target)
    _run(["checkout", "-B", "main"], cwd=target)

    (target / "app").mkdir()
    (target / "app" / "__init__.py").write_text("", encoding="utf-8")
    (target / "app" / "calc.py").write_text(APP_CALC_PY, encoding="utf-8")
    (target / "tests").mkdir()
    (target / "tests" / "__init__.py").write_text("", encoding="utf-8")
    (target / "tests" / "test_calc.py").write_text(TEST_CALC_PY, encoding="utf-8")
    (target / "requirements.txt").write_text(REQUIREMENTS_TXT, encoding="utf-8")
    (target / "README.md").write_text(README_MD, encoding="utf-8")
    (target / ".gitignore").write_text("__pycache__/\n*.pyc\n.venv/\n.pytest_cache/\n", encoding="utf-8")

    agents_dir = target / ".github" / "agents"
    agents_dir.mkdir(parents=True)
    for name in ("dev-throwaway.md", "structural-reviewer.md"):
        shutil.copy(TEMPLATES_DIR / name, agents_dir / name)

    _run(["add", "-A"], cwd=target)
    _run(["commit", "-q", "-m", "initial scaffold: calc.py (with a seeded, unrelated bug) + pytest"], cwd=target)
    _run(["remote", "add", "origin", str(bare)], cwd=target)
    _run(["push", "-u", "origin", "main"], cwd=target)

    return target, bare
