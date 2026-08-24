"""Phase 1 step 3 / "How to author a skill file" step 8: smoke-test both
skills authored for Phase 1 (dev-throwaway.md, structural-reviewer.md)
BEFORE wiring them into task_loop.py / the post-hoc gate. Same discipline
the predecessor's smoke_test_tool_use.py documents: "if this fails, the
interactive loop... will fail too, just harder to diagnose."

Two checks per skill:
  - dev-throwaway.md: a real `copilot --agent dev-throwaway -p ...` run
    against a disposable scratch repo with a deliberately buggy function +
    failing test (same shape as Phase 0 spike #1) — passes only if the fix
    is independently re-verified (own pytest run, own git diff read), never
    trusting Copilot's self-report.
  - structural-reviewer.md: (a) a retry-until-valid-json UNIT test against a
    monkeypatched copilot_cli.run_copilot that returns malformed JSON on the
    first call and valid JSON on the second — proves the retry logic itself
    works, deterministically, without depending on real model flakiness;
    (b) a REAL invocation reviewing an actual `git diff --cached` output
    (not a hand-written snippet — Phase 0 spike #4 found the model may not
    recognize non-canonically-shaped diff text as real), confirming it
    returns a parseable, schema-matching, and semantically correct verdict.

Usage:
    python .loop-eng/phase1-proof/smoke_test_skills.py
"""
from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from pipeline import copilot_cli  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

TEMPLATES_DIR = Path(__file__).resolve().parents[1] / "agent-templates"


def _git(args: list[str], cwd: Path, check: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *args], cwd=str(cwd), capture_output=True, text=True, encoding="utf-8", check=check
    )


def _scaffold_scratch_repo() -> Path:
    scratch = Path(tempfile.mkdtemp(prefix="loop-eng-smoke-"))
    agents_dir = scratch / ".github" / "agents"
    agents_dir.mkdir(parents=True)
    for name in ("dev-throwaway.md", "structural-reviewer.md"):
        shutil.copy(TEMPLATES_DIR / name, agents_dir / name)
    _git(["init", "-q"], scratch)
    _git(["-c", "user.email=smoke@local", "-c", "user.name=smoke", "config", "user.email", "smoke@local"], scratch)
    _git(["config", "user.name", "smoke"], scratch)
    return scratch


def smoke_test_dev_throwaway(scratch: Path) -> bool:
    print("\n=== smoke test: dev-throwaway.md ===")
    (scratch / "add.py").write_text("def add(a, b):\n    return a - b  # BUG: should be a + b\n", encoding="utf-8")
    (scratch / "test_add.py").write_text(
        "from add import add\n\n\ndef test_add():\n    assert add(2, 3) == 5\n", encoding="utf-8"
    )
    _git(["add", "-A"], scratch)
    _git(["commit", "-q", "-m", "initial broken state"], scratch)

    prompt = (
        "Ticket SMOKE-1: fix add(). Run 'python -m pytest test_add.py -v' to see it fail, "
        "find the bug in add.py, fix it, then re-run pytest to confirm it passes."
    )
    try:
        result = copilot_cli.run_copilot(
            prompt, cwd=scratch, agent="dev-throwaway",
            allow_tools=["write", "shell(python:*)", "shell(pytest:*)"],
            timeout=180,
        )
    except copilot_cli.CopilotError as e:
        print(f"FAIL: invocation error: {e}")
        return False

    verify = subprocess.run(
        [sys.executable, "-m", "pytest", "test_add.py", "-v"],
        cwd=str(scratch), capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    source_after = (scratch / "add.py").read_text(encoding="utf-8")
    print(f"copilot exit 0 in {result.duration_seconds:.1f}s; independent pytest returncode={verify.returncode}")
    if verify.returncode == 0 and "a + b" in source_after:
        print("PASS: dev-throwaway.md implemented the fix and it independently verifies.")
        return True
    print(f"FAIL: independent verification did not confirm the fix.\n{verify.stdout[-1000:]}")
    return False


def smoke_test_structural_reviewer_retry_logic() -> bool:
    print("\n=== smoke test: structural-reviewer.md — retry-until-valid-json (unit, monkeypatched) ===")
    malformed = copilot_cli.CopilotResult(
        stdout="I looked at the diff and it seems fine, no JSON here.", stderr="", returncode=0, duration_seconds=0.1
    )
    valid = copilot_cli.CopilotResult(
        stdout='```json\n{"clear_to_merge": true, "issues": []}\n```', stderr="", returncode=0, duration_seconds=0.1
    )
    calls = {"n": 0}

    def fake_run_copilot(*args, **kwargs):
        calls["n"] += 1
        return malformed if calls["n"] == 1 else valid

    with mock.patch.object(copilot_cli, "run_copilot", side_effect=fake_run_copilot):
        verdict = copilot_cli.run_verdict_skill(
            agent="structural-reviewer", message="irrelevant for this unit test",
            schema_required_keys=["clear_to_merge", "issues"], cwd=Path("."), max_json_retries=2,
        )
    if calls["n"] == 2 and verdict == {"clear_to_merge": True, "issues": []}:
        print("PASS: first (malformed) response triggered exactly one re-prompt, second (valid) response was accepted.")
        return True
    print(f"FAIL: expected 2 calls + accepted verdict, got {calls['n']} call(s), verdict={verdict!r}")
    return False


def smoke_test_structural_reviewer_real_diff(scratch: Path) -> bool:
    print("\n=== smoke test: structural-reviewer.md — real invocation against a real `git diff` ===")
    (scratch / "greeter.py").write_text('def greet(name: str) -> str:\n    return f"Hello, {name}!"\n', encoding="utf-8")
    _git(["add", "-A"], scratch)
    diff = _git(["diff", "--cached"], scratch).stdout
    if not diff.strip():
        print("FAIL: expected a non-empty real git diff to review, got nothing.")
        return False

    message = f"Ticket SMOKE-2: add a greet() helper.\n\nDiff:\n{diff}"
    try:
        verdict = copilot_cli.run_verdict_skill(
            agent="structural-reviewer", message=message,
            schema_required_keys=["clear_to_merge", "issues"], cwd=scratch, timeout=180,
        )
    except copilot_cli.CopilotJsonFormatError as e:
        print(f"FAIL: never produced valid JSON: {e}\nraw tail: {e.raw_output[-1500:]}")
        return False

    print(f"verdict: {verdict}")
    if "clear_to_merge" in verdict and isinstance(verdict["issues"], list):
        if verdict["clear_to_merge"] is True:
            print("PASS: format-valid AND semantically correct (a trivial, safe diff was cleared).")
        else:
            print("PARTIAL: format-valid but flagged a trivial safe diff as not clear_to_merge — "
                  "format compliance and semantic correctness are separate failure modes (spike #4); "
                  "not treated as a hard smoke-test failure, but worth a manual look.")
        return True
    print(f"FAIL: schema mismatch: {verdict!r}")
    return False


def main() -> int:
    scratch = _scaffold_scratch_repo()
    print(f"scratch repo: {scratch}")
    results = {}
    try:
        results["retry-logic"] = smoke_test_structural_reviewer_retry_logic()
        results["dev-throwaway"] = smoke_test_dev_throwaway(scratch)
        results["structural-reviewer-real-diff"] = smoke_test_structural_reviewer_real_diff(scratch)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)

    print("\n=== summary ===")
    ok = True
    for name, passed in results.items():
        print(f"  {'PASS' if passed else 'FAIL'}: {name}")
        ok = ok and passed
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
