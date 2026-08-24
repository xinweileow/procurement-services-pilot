"""Phase 0 spike #7 (plan/dev-suite-plans): what exact file format and folder
location does Copilot expect for a custom persona/agent definition? This
plan's `skills/*.md` files were designed by analogy to Claude Code's SKILL.md
format -- unverified until this spike runs.

Verifies the format empirically: writes a minimal agent definition to
`.github/agents/<name>.md` with `name`/`description` frontmatter (the
convention `[REF]_etiqa_agent/dotnet-api.agent.md` itself pointed at), then
invokes it via `copilot --agent <name> -p ...` and confirms the persona body
was actually followed (not just that the flag didn't error).

Usage:
    python .loop-eng/phase0-spikes/spike_07_agent_file_format.py
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

AGENT_MD = """---
name: spike-test-agent
description: A minimal test agent to probe Copilot CLI's agent discovery format.
---

You are a test agent. When asked anything, reply with exactly the string: AGENT_LOADED_OK
"""


def main() -> int:
    scratch = Path(tempfile.mkdtemp(prefix="spike07-"))
    try:
        agents_dir = scratch / ".github" / "agents"
        agents_dir.mkdir(parents=True)
        (agents_dir / "spike-test-agent.md").write_text(AGENT_MD, encoding="utf-8")

        result = subprocess.run(
            [COPILOT, "--agent", "spike-test-agent", "-p", "say hi", "--allow-tool", "write"],
            cwd=scratch, capture_output=True, text=True, encoding="utf-8", timeout=60,
        )
        print("--- stdout ---")
        print(result.stdout)

        if result.returncode == 0 and "AGENT_LOADED_OK" in result.stdout:
            print(
                "PASS: confirmed format --\n"
                "  Location:    .github/agents/<agent-name>.md\n"
                "  Frontmatter: YAML, at minimum `name:` + `description:`\n"
                "  Invocation:  copilot --agent <agent-name> -p \"...\"\n"
                "  Body:        everything after the frontmatter is followed as the persona.\n"
                "This is the real format for skills/*.md in Phase 2 -- update the plan's "
                "provisional [REF]_etiqa_agent-shaped frontmatter placeholder to match."
            )
            return 0
        print("FAIL: agent was not loaded / persona not followed -- format assumption wrong, investigate further.")
        return 1
    finally:
        shutil.rmtree(scratch, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
