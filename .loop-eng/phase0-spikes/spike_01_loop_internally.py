"""Phase 0 spike #1 (plan/dev-suite-plans): does `copilot -p` loop internally
(edit -> test -> fix -> repeat) inside one invocation, or does it stop after a
single tool call? This is the highest-leverage unknown in the plan -- the
entire Full flow and Phase 1 design assumes "yes, it loops."

Sets up a disposable scratch repo with a deliberately buggy function and a
failing test, then runs ONE `copilot -p` invocation asking it to make the
test pass. If the bug is actually fixed and the test genuinely passes
afterward (verified independently, not by trusting Copilot's own report),
that's proof the CLI iterates test->fix->retest without external
orchestration between steps.

Usage:
    python .loop-eng/phase0-spikes/spike_01_loop_internally.py
"""
from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

# Same fix task_loop.py already documents for the real pipeline: Windows
# stdout defaults to cp1252 once redirected, and Copilot's own output can
# contain Unicode (box-drawing chars, em dashes) cp1252 can't encode.
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

# subprocess.run(["copilot", ...]) fails on Windows with FileNotFoundError --
# npm installs it as copilot.CMD, and CreateProcess (unlike cmd.exe) doesn't
# resolve PATHEXT shims from a bare name. Resolve the real path once.
COPILOT = shutil.which("copilot") or "copilot"

BUGGY_SOURCE = (
    "def add(a, b):\n"
    "    return a - b  # BUG: should be a + b\n"
)

TEST_SOURCE = (
    "from add import add\n\n"
    "def test_add():\n"
    "    assert add(2, 3) == 5\n"
)

PROMPT = (
    "Run 'python -m pytest test_add.py -v' to see it fail, find the bug in "
    "add.py, fix it, then re-run pytest to confirm it passes. Report PASS or "
    "FAIL at the end."
)


def main() -> int:
    scratch = Path(tempfile.mkdtemp(prefix="spike01-"))
    try:
        (scratch / "add.py").write_text(BUGGY_SOURCE, encoding="utf-8")
        (scratch / "test_add.py").write_text(TEST_SOURCE, encoding="utf-8")
        subprocess.run(["git", "init", "-q"], cwd=scratch, check=True)
        subprocess.run(["git", "add", "-A"], cwd=scratch, check=True)
        subprocess.run(
            ["git", "-c", "user.email=spike@local", "-c", "user.name=spike", "commit", "-q", "-m", "initial broken state"],
            cwd=scratch, check=True,
        )

        result = subprocess.run(
            [
                COPILOT, "-p", PROMPT,
                "--allow-tool", "write",
                "--allow-tool", "shell(python:*)",
                "--allow-tool", "shell(pytest:*)",
                "-s",
            ],
            cwd=scratch, capture_output=True, text=True, encoding="utf-8", timeout=180,
        )
        print("--- copilot stdout ---")
        print(result.stdout)
        if result.returncode != 0:
            print(f"FAIL: copilot exited {result.returncode}\n{result.stderr}")
            return 1

        # Never trust Copilot's own report -- independently re-run pytest,
        # same discipline the real pipeline's post-hoc gate uses.
        verify = subprocess.run(
            [sys.executable, "-m", "pytest", "test_add.py", "-v"],
            cwd=scratch, capture_output=True, text=True, encoding="utf-8", timeout=60,
        )
        print("--- independent pytest verification ---")
        print(verify.stdout)

        source_after = (scratch / "add.py").read_text(encoding="utf-8")
        if verify.returncode == 0 and "a + b" in source_after:
            print("PASS: single `copilot -p` invocation ran pytest, read the "
                  "source, fixed the bug, and re-ran pytest to confirm -- no "
                  "external orchestration between steps.")
            return 0
        print("FAIL: test still failing or bug not actually fixed after the "
              "single invocation -- copilot -p does NOT reliably loop "
              "internally. See plan/dev-suite-plans Phase 0 spike #1's "
              "documented fallback (stage-by-stage orchestration).")
        return 1
    finally:
        shutil.rmtree(scratch, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
