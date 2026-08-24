"""Real, quota-independent test of `.loop-eng/hooks/pre-push`: installs the
actual hook into a scratch repo (built the same way as the rest of Phase 1's
proof — see scratch_repo.py), then does REAL `git push` calls against a
REAL local bare remote and checks the hook actually blocks/allows them.

Three cases:
  1. A clean commit -> push allowed.
  2. A commit that plants a fake secret -> push BLOCKED (secrets scan over
     the actual push range, not the staged index -- see
     hooks/pre_push_check.py's docstring for why that distinction matters).
  3. A commit that breaks the regression suite -> push BLOCKED.
And a control: the same secret-planting commit pushed with `--no-verify`
succeeds, proving the hook's known bypassability (pre-flight item 5) is
real and not accidentally un-bypassable in a way that would misrepresent
what's actually built.

Usage:
    python .loop-eng/phase1-proof/test_pre_push_hook.py
"""
from __future__ import annotations

import shutil
import stat
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from scratch_repo import build_scratch_repo  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

LOOP_ENG_ROOT = Path(__file__).resolve().parents[1]
HOOK_SRC = LOOP_ENG_ROOT / "hooks" / "pre-push"


def _run(args: list[str], cwd: Path, check: bool = False) -> subprocess.CompletedProcess:
    return subprocess.run(args, cwd=str(cwd), capture_output=True, text=True, encoding="utf-8", check=check)


def _vendor_loop_eng_into_target_repo(target_dir: Path) -> None:
    """The plan's real deployment model has .loop-eng "cloned/submoduled
    into each target repo" -- scratch_repo.py's scaffold deliberately
    doesn't do this (Phase 1's synthetic-ticket proof runs the pipeline
    orchestrator-side, standalone). This hook test is the one place that
    needs the real layout, so it vendors pipeline/ + hooks/ into the
    scratch target repo directly, same as a real submodule add + commit
    would end up looking like on disk."""
    dest_root = target_dir / ".loop-eng"
    shutil.copytree(LOOP_ENG_ROOT / "pipeline", dest_root / "pipeline", ignore=shutil.ignore_patterns("__pycache__"))
    shutil.copytree(LOOP_ENG_ROOT / "hooks", dest_root / "hooks")
    subprocess.run(["git", "add", "-A"], cwd=str(target_dir), check=True)
    subprocess.run(["git", "commit", "-q", "-m", "vendor .loop-eng for hook test"], cwd=str(target_dir), check=True)
    subprocess.run(["git", "push", "origin", "main"], cwd=str(target_dir), check=True)


def _install_hook(target_dir: Path) -> None:
    dest = target_dir / ".git" / "hooks" / "pre-push"
    shutil.copy(HOOK_SRC, dest)
    dest.chmod(dest.stat().st_mode | stat.S_IEXEC | stat.S_IXGRP | stat.S_IXOTH)


def case_clean_push_allowed(target_dir: Path) -> bool:
    print("\n=== case 1: clean commit -> push allowed ===")
    _run(["git", "checkout", "-B", "feature/clean"], target_dir, check=True)
    # scratch_repo.py's baseline ships subtract() with a bug SEEDED ON
    # PURPOSE (needed elsewhere, for the coding-agent's regression-retry
    # proof) -- a genuinely "clean" push here has to fix that first, or
    # this case would just be case 3 in disguise and prove nothing new.
    calc = target_dir / "app" / "calc.py"
    calc.write_text(calc.read_text(encoding="utf-8").replace(
        "return a + b  # BUG: should be a - b -- deliberately seeded, not part of the ticket\n",
        "return a - b\n", 1,
    ), encoding="utf-8")
    (target_dir / "app" / "clean_addition.py").write_text("def noop() -> None:\n    return None\n", encoding="utf-8")
    _run(["git", "add", "-A"], target_dir, check=True)
    _run(["git", "commit", "-q", "-m", "clean addition"], target_dir, check=True)
    result = _run(["git", "push", "-u", "origin", "feature/clean"], target_dir)
    print(result.stdout[-800:] + result.stderr[-800:])
    if result.returncode == 0:
        print("PASS: clean push was allowed.")
        return True
    print("FAIL: clean push was blocked, expected it to succeed.")
    return False


def case_secret_push_blocked(target_dir: Path) -> bool:
    print("\n=== case 2: commit with a planted secret -> push BLOCKED ===")
    _run(["git", "checkout", "main"], target_dir, check=True)
    _run(["git", "checkout", "-B", "feature/leaky"], target_dir, check=True)
    (target_dir / "app" / "leaky.py").write_text(
        'AWS_KEY = "AKIAABCDEFGHIJKLMNOP"  # planted for this test\n', encoding="utf-8"
    )
    _run(["git", "add", "-A"], target_dir, check=True)
    _run(["git", "commit", "-q", "-m", "oops, a secret"], target_dir, check=True)
    result = _run(["git", "push", "-u", "origin", "feature/leaky"], target_dir)
    print(result.stdout[-800:] + result.stderr[-800:])
    if result.returncode != 0 and "secrets scan" in (result.stdout + result.stderr):
        print("PASS: push with a planted secret was blocked, and blocked for the right reason.")
        return True
    print(f"FAIL: expected the push to be blocked citing the secrets scan (returncode={result.returncode}).")
    return False


def case_broken_regression_blocked(target_dir: Path) -> bool:
    print("\n=== case 3: commit that breaks the regression suite -> push BLOCKED ===")
    _run(["git", "checkout", "main"], target_dir, check=True)
    _run(["git", "checkout", "-B", "feature/broken"], target_dir, check=True)
    calc = target_dir / "app" / "calc.py"
    calc.write_text(calc.read_text(encoding="utf-8").replace("return a + b\n", "return a - b  # oops\n", 1), encoding="utf-8")
    _run(["git", "add", "-A"], target_dir, check=True)
    _run(["git", "commit", "-q", "-m", "accidentally break add()"], target_dir, check=True)
    result = _run(["git", "push", "-u", "origin", "feature/broken"], target_dir)
    print(result.stdout[-1200:] + result.stderr[-800:])
    if result.returncode != 0 and "regression suite" in (result.stdout + result.stderr):
        print("PASS: push that breaks the regression suite was blocked, and blocked for the right reason.")
        return True
    print(f"FAIL: expected the push to be blocked citing the regression suite (returncode={result.returncode}).")
    return False


def case_no_verify_bypasses(target_dir: Path) -> bool:
    print("\n=== control: same leaky commit, pushed with --no-verify -> succeeds (documents the known bypass) ===")
    result = _run(["git", "push", "--no-verify", "-u", "origin", "feature/leaky"], target_dir)
    print(result.stdout[-400:] + result.stderr[-400:])
    if result.returncode == 0:
        print("PASS (expected/documented): --no-verify bypasses the local hook, confirming pre-flight item 5's gap is real.")
        return True
    print("FAIL: --no-verify should have bypassed the hook but the push still failed.")
    return False


def main() -> int:
    root = Path(tempfile.mkdtemp(prefix="loop-eng-hook-test-"))
    print(f"scratch root: {root}")
    try:
        target_dir, bare_remote = build_scratch_repo(root)
        _vendor_loop_eng_into_target_repo(target_dir)
        _install_hook(target_dir)

        results = {
            "clean push allowed": case_clean_push_allowed(target_dir),
            "secret-leaking push blocked": case_secret_push_blocked(target_dir),
            "regression-breaking push blocked": case_broken_regression_blocked(target_dir),
            "--no-verify bypasses (documented gap)": case_no_verify_bypasses(target_dir),
        }
    finally:
        shutil.rmtree(root, ignore_errors=True)

    print("\n=== summary ===")
    ok = True
    for name, passed in results.items():
        print(f"  {'PASS' if passed else 'FAIL'}: {name}")
        ok = ok and passed
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
