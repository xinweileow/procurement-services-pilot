"""Phase 0 spike #2 (plan/dev-suite-plans): can `copilot -p` run with no
interactive login -- blocks everything in Phase 1/2 if not, since the whole
pipeline runs unattended.

`copilot help environment` documents that COPILOT_GITHUB_TOKEN, GH_TOKEN, and
GITHUB_TOKEN (checked in that precedence order) are read as an alternative to
the interactive OAuth flow, explicitly for "headless" automation use. This
spike verifies that claim empirically: strip the CLI's own credential store
out of the child process's environment, supply only a token via env var, and
confirm `copilot -p` still authenticates and answers.

Usage:
    python .loop-eng/phase0-spikes/spike_02_headless_auth.py
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

TOKEN_ENV_VARS = ["COPILOT_GITHUB_TOKEN", "GH_TOKEN", "GITHUB_TOKEN"]
COPILOT = shutil.which("copilot") or "copilot"
GH = shutil.which("gh") or "gh"


def main() -> int:
    token = next((os.environ[v] for v in TOKEN_ENV_VARS if os.environ.get(v)), None)
    if token is None:
        # gh CLI already has a token on this machine (see `gh auth status`) --
        # reuse it so this spike doesn't require a separate token to be
        # provisioned just to run the check.
        gh_token = subprocess.run(
            [GH, "auth", "token"], capture_output=True, text=True, encoding="utf-8"
        )
        if gh_token.returncode != 0 or not gh_token.stdout.strip():
            print(
                "FAIL: no token found via COPILOT_GITHUB_TOKEN/GH_TOKEN/GITHUB_TOKEN "
                "and `gh auth token` failed -- cannot test headless auth without one."
            )
            return 1
        token = gh_token.stdout.strip()

    # Build a minimal, isolated environment: only what a real headless CI/cron
    # runner would have -- no COPILOT_* credential-store state, just the token.
    env = {
        "PATH": os.environ.get("PATH", ""),
        "GH_TOKEN": token,
    }
    if os.name == "nt":
        # copilot's Node runtime needs these on Windows to resolve npm's shim
        for var in ("APPDATA", "LOCALAPPDATA", "USERPROFILE", "SYSTEMROOT"):
            if os.environ.get(var):
                env[var] = os.environ[var]

    result = subprocess.run(
        [COPILOT, "-p", "reply with exactly: HEADLESS_AUTH_OK", "--allow-tool", "write", "-s"],
        capture_output=True, text=True, encoding="utf-8", timeout=60, env=env,
    )
    print("--- stdout ---")
    print(result.stdout)
    print("--- stderr ---")
    print(result.stderr)

    if result.returncode == 0 and "HEADLESS_AUTH_OK" in result.stdout:
        print("PASS: copilot -p authenticated and ran with only GH_TOKEN in "
              "env, no interactive login, no pre-existing COPILOT_* credential "
              "store state passed through.")
        return 0
    print("FAIL: headless env-var auth did not work as documented -- "
          "investigate before assuming Phase 1/2 automation can run unattended.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
