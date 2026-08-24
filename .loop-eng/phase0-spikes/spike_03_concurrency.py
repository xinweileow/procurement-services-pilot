"""Phase 0 spike #3 (plan/dev-suite-plans): do parallel `copilot -p`
invocations (mirroring today's hotfix worktree race, run_hotfix_race in
task_loop.py) collide on session/credential state?

Fires N `copilot -p` invocations concurrently, each in its own scratch repo,
each asked to write a file containing a unique marker, and checks that every
invocation's output/file matches its OWN marker -- not another invocation's,
which would indicate session/credential/output cross-contamination under
concurrency.

Usage:
    python .loop-eng/phase0-spikes/spike_03_concurrency.py [--n 3]
"""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

COPILOT = shutil.which("copilot") or "copilot"


def _run_one(worker_id: int, scratch_root: Path) -> tuple[int, bool, str]:
    scratch = scratch_root / f"worker-{worker_id}"
    scratch.mkdir(parents=True)
    marker = f"WORKER_{worker_id}_MARKER"
    result = subprocess.run(
        [COPILOT, "-p", f'Create a file named marker.txt containing exactly: {marker}',
         "--allow-tool", "write", "-s"],
        cwd=scratch, capture_output=True, text=True, encoding="utf-8", timeout=90,
    )
    marker_file = scratch / "marker.txt"
    content = marker_file.read_text(encoding="utf-8").strip() if marker_file.exists() else "(no file written)"
    ok = result.returncode == 0 and content == marker
    return worker_id, ok, content


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--n", type=int, default=3, help="number of concurrent invocations")
    args = parser.parse_args()

    scratch_root = Path(tempfile.mkdtemp(prefix="spike03-"))
    try:
        results: list[tuple[int, bool, str]] = []
        with ThreadPoolExecutor(max_workers=args.n) as pool:
            futures = [pool.submit(_run_one, i, scratch_root) for i in range(args.n)]
            for f in as_completed(futures):
                results.append(f.result())

        results.sort()
        all_ok = True
        for worker_id, ok, content in results:
            print(f"worker {worker_id}: {'OK' if ok else 'FAIL'} -> {content!r}")
            all_ok = all_ok and ok

        if all_ok:
            print(f"\nPASS: {args.n} concurrent copilot -p invocations each wrote "
                  "their own correct marker -- no cross-contamination observed.")
            return 0
        print(f"\nFAIL: at least one worker's output didn't match its own marker -- "
              "possible session/credential/output collision under concurrency. "
              "Investigate before relying on the hotfix N-sandbox race pattern.")
        return 1
    finally:
        shutil.rmtree(scratch_root, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
