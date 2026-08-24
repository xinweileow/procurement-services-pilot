"""Standalone N-run reliability check for structural-reviewer.md against a
real `git diff`, same methodology as Phase 0 spike #4 (run it several times,
report the pass rate — a single run can't distinguish "reliable" from "got
lucky"). Split out of smoke_test_skills.py so the reviewer persona can be
re-verified in isolation (cheap, no dev-throwaway.md invocation needed)
after any future wording change, without re-running the slower full smoke
suite. Needs live Copilot quota — see phase1-proof/README.md for status.

Usage:
    python .loop-eng/phase1-proof/retest_reviewer.py [N]   # default N=3
"""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pipeline import copilot_cli  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TEMPLATES_DIR = Path(__file__).resolve().parents[1] / "agent-templates"


def _git(args, cwd, check=True):
    return subprocess.run(["git", *args], cwd=str(cwd), capture_output=True, text=True, encoding="utf-8", check=check)


def run_once(i: int) -> bool:
    scratch = Path(tempfile.mkdtemp(prefix=f"loop-eng-retest-{i}-"))
    try:
        agents_dir = scratch / ".github" / "agents"
        agents_dir.mkdir(parents=True)
        shutil.copy(TEMPLATES_DIR / "structural-reviewer.md", agents_dir / "structural-reviewer.md")
        _git(["init", "-q"], scratch)
        _git(["config", "user.email", "smoke@local"], scratch)
        _git(["config", "user.name", "smoke"], scratch)
        (scratch / "greeter.py").write_text('def greet(name: str) -> str:\n    return f"Hello, {name}!"\n', encoding="utf-8")
        _git(["add", "-A"], scratch)
        diff = _git(["diff", "--cached"], scratch).stdout
        message = f"Ticket SMOKE-2: add a greet() helper.\n\nDiff:\n{diff}"
        try:
            verdict = copilot_cli.run_verdict_skill(
                agent="structural-reviewer", message=message,
                schema_required_keys=["clear_to_merge", "issues"], cwd=scratch, timeout=180,
            )
        except copilot_cli.CopilotJsonFormatError as e:
            print(f"[run {i}] FAIL: {e}\nraw tail: {e.raw_output[-1200:]}")
            return False
        print(f"[run {i}] verdict: {verdict}")
        return "clear_to_merge" in verdict and isinstance(verdict.get("issues"), list)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)


if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 3
    results = [run_once(i) for i in range(1, n + 1)]
    print(f"\n{sum(results)}/{n} passed")
    raise SystemExit(0 if all(results) else 1)
