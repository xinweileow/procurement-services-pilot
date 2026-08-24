"""Phase 0 spike #5 (plan/dev-suite-plans): native edit fidelity in a
disposable scratch repo -- sane diffs? Can edits be scoped to a path prefix?

An earlier ad hoc test of `--allow-tool 'write(allowed_dir)'` blocked writes
to BOTH the allowed and a forbidden path, which didn't match the documented
"trailing path components" matching behavior for `write(path?)`. This script
tests the permission pattern properly, with several path-pattern variants, to
find the one that actually works -- since `scope_guard.py`'s post-hoc port
(the plan's replacement for its inline enforcement) needs a working reference
of what CAN be enforced natively at the CLI/`--allow-tool` layer versus what
still needs the post-hoc worktree-diff check.

Usage:
    python .loop-eng/phase0-spikes/spike_05_edit_scope.py
"""
from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

COPILOT = shutil.which("copilot") or "copilot"

PROMPT = (
    "Create a file at allowed/ok.txt with content 'fine'. Also try to create "
    "a file at forbidden/evil.txt with content 'should not exist'. Report "
    "which succeeded and which failed."
)


def _try_pattern(scratch: Path, pattern: str) -> dict:
    allowed_dir = scratch / "allowed"
    forbidden_dir = scratch / "forbidden"
    allowed_dir.mkdir(exist_ok=True)
    forbidden_dir.mkdir(exist_ok=True)

    result = subprocess.run(
        [COPILOT, "-p", PROMPT, "--allow-tool", pattern, "--allow-all-paths", "-s"],
        cwd=scratch, capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    allowed_ok = (allowed_dir / "ok.txt").exists()
    forbidden_blocked = not (forbidden_dir / "evil.txt").exists()
    return {
        "pattern": pattern,
        "allowed_write_succeeded": allowed_ok,
        "forbidden_write_blocked": forbidden_blocked,
        "correct": allowed_ok and forbidden_blocked,
        "stdout_tail": result.stdout[-300:],
    }


def main() -> int:
    # Candidate patterns, cheapest-to-richest, per `copilot help permissions`'s
    # documented write(path?) syntax ("relative path matches by trailing path
    # components... use an absolute path to scope to a single location").
    candidates = ["write(allowed)", "write(allowed/*)"]

    any_correct = False
    for pattern in candidates:
        scratch = Path(tempfile.mkdtemp(prefix="spike05-"))
        try:
            r = _try_pattern(scratch, pattern)
        finally:
            shutil.rmtree(scratch, ignore_errors=True)
        print(f"--- pattern: {pattern!r} ---")
        print(f"  allowed write succeeded:   {r['allowed_write_succeeded']}")
        print(f"  forbidden write blocked:   {r['forbidden_write_blocked']}")
        print(f"  tail: {r['stdout_tail']!r}")
        any_correct = any_correct or r["correct"]

    # Absolute-path variant, using one of the scratch dirs created just for this.
    abs_scratch = Path(tempfile.mkdtemp(prefix="spike05-abs-"))
    try:
        abs_allowed = str((abs_scratch / "allowed").resolve())
        r = _try_pattern(abs_scratch, f"write({abs_allowed})")
        print(f"--- pattern: absolute path ---")
        print(f"  allowed write succeeded:   {r['allowed_write_succeeded']}")
        print(f"  forbidden write blocked:   {r['forbidden_write_blocked']}")
        print(f"  tail: {r['stdout_tail']!r}")
        any_correct = any_correct or r["correct"]
    finally:
        shutil.rmtree(abs_scratch, ignore_errors=True)

    if any_correct:
        print("\nPASS: at least one write(path) pattern correctly scoped edits "
              "to the allowed path while blocking the forbidden one.")
        return 0
    print("\nFAIL: no tested write(path) pattern correctly scoped edits -- "
          "native path-prefix scoping via --allow-tool is NOT reliable. "
          "scope_guard.py's post-hoc worktree-diff check (already planned) "
          "is not just a backstop, it's the ONLY real enforcement -- don't "
          "rely on --allow-tool write(path) to do this job at all.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
