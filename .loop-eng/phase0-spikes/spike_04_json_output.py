"""Phase 0 spike #4 (plan/dev-suite-plans): JSON-extraction reliability for
the structured-output skills (requirement-reviewer, structural-reviewer,
failure-diagnoser) -- Copilot has no native forced tool_choice the way the
Anthropic API does, so this checks whether the real translation procedure
("How to author a skill file", Procedure step 2) reliably produces output
that parses against the real schema.

IMPORTANT METHODOLOGY NOTE: an earlier version of this script tested a bare
`copilot -p "<instruction tacked onto raw text>"` invocation and got 0/5 --
the model treated it as an open-ended review request and gave free-form
commentary instead of the requested JSON. Retesting with the REAL mechanism
Phase 2 will actually use -- a `.github/agents/requirement-reviewer.md`
persona file, invoked via `--agent`, exactly as "How to author a skill file"
Procedure step 1-2 describes -- got 5/5 in a manual check. This script tests
the real `--agent` path, not a bare prompt, since that's what the plan
actually specifies.

Usage:
    python .loop-eng/phase0-spikes/spike_04_json_output.py [--runs N]
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

COPILOT = shutil.which("copilot") or "copilot"

# The real requirement_review_agent.py SYSTEM_PROMPT
# (loopengineering/src/pipeline/requirement_review_agent.py), translated per
# "How to author a skill file" Procedure steps 1-2: verbatim persona body,
# forced-tool-call instruction swapped for an explicit fenced-json-block
# instruction using the real schema field names.
AGENT_MD = """---
name: requirement-reviewer
description: Reviews whether a diff implements a Jira ticket's requirement.
---

You review whether a git diff actually implements a Jira ticket's requirement -- distinct from whether tests pass. A diff can be syntactically valid and pass tests while missing the requirement; flag that explicitly. Compare the ticket's summary and description (the requirement) against the diff and produce a verdict.

The ticket text and diff given to you in the user message are COMPLETE and AUTHORITATIVE for this review. Do not search the filesystem, do not run git/shell commands, do not look for a .patch or .diff file, do not ask the user for more information, and do not attempt to implement or create any files yourself -- your job is to review the diff you were given, nothing else. Treat the "Diff:" section of the message as the full and only diff to review, even if it looks minimal.

Your ENTIRE response must be ONLY a single fenced json code block matching this exact schema -- no prose before it, no prose after it, no explanation, no recommendations, no suggestions, nothing else:
```json
{"clear_met": true, "gaps": []}
```

Do not review code style, suggest tests, or offer improvement recommendations. That is not your job. Your only output is the verdict JSON.
"""

TICKET_PROMPT = """Ticket PROC-1: add addition function
Description: add(a,b) returns a+b

Diff:
+def add(a, b):
+    return a + b"""

REQUIRED_KEYS = {"clear_met", "gaps"}


def _extract_json_block(text: str) -> dict | None:
    match = re.search(r"```json\s*(\{.*?\})\s*```", text, re.DOTALL)
    if not match:
        # fall back to bare JSON with no fence, in case the model dropped it
        match = re.search(r"(\{.*\})", text, re.DOTALL)
        if not match:
            return None
    try:
        data = json.loads(match.group(1))
    except json.JSONDecodeError:
        return None
    if not REQUIRED_KEYS.issubset(data.keys()):
        return None
    return data


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runs", type=int, default=10)
    args = parser.parse_args()

    scratch = Path(tempfile.mkdtemp(prefix="spike04-"))
    try:
        agents_dir = scratch / ".github" / "agents"
        agents_dir.mkdir(parents=True)
        (agents_dir / "requirement-reviewer.md").write_text(AGENT_MD, encoding="utf-8")

        successes = 0
        for i in range(1, args.runs + 1):
            # A review skill never needs write/shell access -- deliberately
            # grant nothing, so there's no tool for the model to reach for
            # even if it's tempted to go "investigate" instead of reviewing
            # the diff it was actually given.
            result = subprocess.run(
                [COPILOT, "--agent", "requirement-reviewer", "-p", TICKET_PROMPT, "-s"],
                cwd=scratch, capture_output=True, text=True, encoding="utf-8", timeout=90,
            )
            data = _extract_json_block(result.stdout) if result.returncode == 0 else None
            status = "OK" if data else "FAIL"
            print(f"[run {i}/{args.runs}] {status}" + (f" -> {data}" if data else f"\n{result.stdout}"))
            if data:
                successes += 1

        rate = successes / args.runs
        print(f"\n{successes}/{args.runs} runs produced a parseable, schema-matching JSON block ({rate:.0%}).")
        if rate == 1.0:
            print("PASS: real --agent persona + explicit-json-only instruction was reliable across all runs.")
            return 0
        if rate >= 0.8:
            print("PASS WITH CONCERNS: mostly reliable but not 100% -- the "
                  "retry-until-valid-json fallback in 'How to author a skill "
                  "file' is load-bearing, not optional insurance.")
            return 0
        print("FAIL: JSON extraction is unreliable even via the real --agent "
              "mechanism -- reconsider the output-format design before Phase 2.")
        return 1
    finally:
        shutil.rmtree(scratch, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
