"""Phase 0 spike #8 (plan/dev-suite-plans): does `copilot -p` support pinning
which underlying model an invocation uses, and which models are actually
available under this org's Copilot seats? Added after the user clarified the
org has no Anthropic API access at all -- Copilot's own multi-model support
is the only lever for spreading correlated-failure risk across models
(e.g. running structural-reviewer.md on a different model than
developer-*.md).

`copilot help config` documents a real catalog of settable model names
(claude-sonnet-5, gpt-5.4, gemini-3.7-flash, etc.) -- this checks which of
those actually work via `--model` under the currently authenticated account,
versus only `--model auto` (Copilot's own router) being available.

Usage:
    python .loop-eng/phase0-spikes/spike_08_model_pinning.py
"""
from __future__ import annotations

import shutil
import subprocess
import sys

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

COPILOT = shutil.which("copilot") or "copilot"

# Sample from copilot help config's documented catalog -- not exhaustive,
# enough to tell "nothing pins" from "some models pin, some don't."
CANDIDATE_MODELS = [
    "claude-sonnet-5",
    "claude-haiku-4.5",
    "claude-opus-5",
    "gpt-5.4",
    "gpt-5-mini",
    "gemini-3.7-flash",
    "auto",
]


def main() -> int:
    working: list[str] = []
    failing: list[str] = []
    for model in CANDIDATE_MODELS:
        result = subprocess.run(
            [COPILOT, "-p", "reply with just the word OK", "--model", model, "-s"],
            capture_output=True, text=True, encoding="utf-8", timeout=60,
        )
        ok = result.returncode == 0 and "OK" in result.stdout
        (working if ok else failing).append(model)
        print(f"{'OK  ' if ok else 'FAIL'} --model {model}" + ("" if ok else f"  ({result.stdout.strip() or result.stderr.strip()})"))

    print(f"\nWorking: {working}")
    print(f"Failing: {failing}")

    pinned_non_auto = [m for m in working if m != "auto"]
    if pinned_non_auto:
        print(
            f"PASS: explicit model pinning works for at least {pinned_non_auto} -- "
            "structural-reviewer.md and developer-*.md CAN be deliberately put on "
            "different models for correlated-failure mitigation, per Phase 0 spike #8's design intent."
        )
        return 0
    if "auto" in working:
        print(
            "PARTIAL: only `--model auto` works under this account/entitlement -- every "
            "explicit model name tested was rejected. The multi-model resilience mitigation "
            "from the 'no Anthropic access' decision is NOT achievable via explicit pinning "
            "as this account is currently entitled. Two things to check before concluding "
            "this is final: (1) is this the org's real enterprise Copilot seat, or a more "
            "limited individual one; (2) does a different/updated Copilot CLI version or "
            "plan tier unlock explicit selection. If confirmed permanent, the plan's Phase 0 "
            "spike #8 resolution and the meta-lesson bullet in Reference implementations need "
            "revisiting -- auto-routing already does vary the model per request based on task "
            "complexity, which is a weaker, non-deterministic version of the same mitigation."
        )
        return 1
    print("FAIL: not even --model auto worked -- something more fundamental is broken (auth? CLI version?).")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
