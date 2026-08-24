"""Real, quota-independent test of the server-side CI counterpart to the
local pre-push hook (`.loop-eng/hooks/ci_check.py`, pre-flight item 5's "CI
re-check" half). `check_push()` itself is already proven end-to-end via a
real `git push` in `test_pre_push_hook.py` (both the local hook and
`ci_check.py` call the same function, not two copies) — this test's job is
proving `ci_check.py`'s own new logic: resolving the right (local_sha,
remote_sha) pair from a GitHub Actions event's environment variables for
both `push` and `pull_request` events, since CI has no `git push` stdin to
read that pair from the way the local hook does.

Three cases:
  1. `push` event, clean commit -> allowed (returncode 0).
  2. `push` event, commit with a planted secret -> blocked, citing the
     secrets scan.
  3. `pull_request` event (GITHUB_BASE_SHA/GITHUB_HEAD_SHA instead of
     GITHUB_EVENT_BEFORE/GITHUB_SHA) against the same secret -> also
     blocked, proving the pull_request branch of `_event_shas()` is used
     and not silently falling through to the push-event keys.

Usage:
    python .loop-eng/phase1-proof/test_ci_check.py
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from scratch_repo import build_scratch_repo  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

LOOP_ENG_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(LOOP_ENG_ROOT / "hooks"))
import ci_check  # noqa: E402


def _git(args: list[str], cwd: Path) -> subprocess.CompletedProcess:
    result = subprocess.run(["git", *args], cwd=str(cwd), capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed (cwd={cwd}):\n{result.stdout}\n{result.stderr}")
    return result


def _rev_parse(ref: str, cwd: Path) -> str:
    return _git(["rev-parse", ref], cwd).stdout.strip()


def case_push_event_clean(target_dir: Path) -> bool:
    print("\n=== case 1: push event, clean commit -> allowed ===")
    before = _rev_parse("HEAD", target_dir)
    calc = target_dir / "app" / "calc.py"
    calc.write_text(calc.read_text(encoding="utf-8").replace(
        "return a + b  # BUG: should be a - b -- deliberately seeded, not part of the ticket\n",
        "return a - b\n", 1,
    ), encoding="utf-8")
    _git(["add", "-A"], target_dir)
    _git(["commit", "-q", "-m", "clean fix"], target_dir)
    after = _rev_parse("HEAD", target_dir)

    env = {"GITHUB_EVENT_NAME": "push", "GITHUB_SHA": after, "GITHUB_EVENT_BEFORE": before}
    with mock.patch.dict(os.environ, env, clear=False):
        rc = ci_check.main(repo_root=target_dir)
    print("PASS" if rc == 0 else f"FAIL (returncode={rc}, expected the clean push to be allowed)")
    return rc == 0


def case_push_event_secret_blocked(target_dir: Path) -> tuple[bool, str, str]:
    print("\n=== case 2: push event, commit with a planted secret -> blocked ===")
    _git(["checkout", "main"], target_dir)
    before = _rev_parse("HEAD", target_dir)
    _git(["checkout", "-B", "feature/leaky-ci", "main"], target_dir)
    (target_dir / "app" / "leaky_ci.py").write_text(
        'AWS_KEY = "AKIAABCDEFGHIJKLMNOP"  # planted for this test\n', encoding="utf-8",
    )
    _git(["add", "-A"], target_dir)
    _git(["commit", "-q", "-m", "oops, a secret (ci check)"], target_dir)
    after = _rev_parse("HEAD", target_dir)

    env = {"GITHUB_EVENT_NAME": "push", "GITHUB_SHA": after, "GITHUB_EVENT_BEFORE": before}
    with mock.patch.dict(os.environ, env, clear=False):
        rc = ci_check.main(repo_root=target_dir)
    ok = rc != 0
    print("PASS" if ok else "FAIL (expected the push event to be blocked)")
    return ok, before, after


def case_pull_request_event_secret_blocked(target_dir: Path, base_sha: str, head_sha: str) -> bool:
    print("\n=== case 3: pull_request event, same secret -> blocked via base/head resolution ===")
    env = {"GITHUB_EVENT_NAME": "pull_request", "GITHUB_BASE_SHA": base_sha, "GITHUB_HEAD_SHA": head_sha}
    # GITHUB_SHA/GITHUB_EVENT_BEFORE deliberately left unset here -- if
    # _event_shas() fell through to the push-event keys by mistake this
    # would KeyError, not silently pass.
    with mock.patch.dict(os.environ, env, clear=False):
        rc = ci_check.main(repo_root=target_dir)
    ok = rc != 0
    print("PASS" if ok else "FAIL (expected the pull_request event to be blocked)")
    return ok


def main() -> int:
    root = Path(tempfile.mkdtemp(prefix="loop-eng-ci-check-test-"))
    print(f"scratch root: {root}")
    try:
        target_dir, _bare = build_scratch_repo(root)

        results = {"push event, clean commit -> allowed": case_push_event_clean(target_dir)}

        secret_ok, base_sha, head_sha = case_push_event_secret_blocked(target_dir)
        results["push event, secret -> blocked"] = secret_ok
        results["pull_request event, secret -> blocked"] = case_pull_request_event_secret_blocked(
            target_dir, base_sha, head_sha,
        )
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
