"""The coding-agent engine: wraps `copilot -p` / `copilot --agent <name> -p`
invocations. Every flag/quirk encoded here was independently verified against
a real `copilot` CLI 1.0.80 invocation in Phase 0 (see
.loop-eng/phase0-spikes/FINDINGS.md) — nothing here is speculative.

Confirmed, load-bearing findings this module encodes:
- Windows: `subprocess.run(["copilot", ...])` raises FileNotFoundError — npm
  installs it as `copilot.CMD`, and CreateProcess doesn't resolve PATHEXT
  shims from a bare name. Resolve via `shutil.which("copilot")` first.
- Real flag syntax: `--allow-tool write --allow-tool shell` (not "edit").
- `--model auto` is the only value accepted under this account (spike #8).
- Verdict-shaped skills must get ZERO tool access (no --allow-tool at all) —
  granting write/shell, even to a reviewer persona, makes the model go
  hunting the filesystem for "the real diff" instead of reviewing the one
  it was given (spike #4, 0/5 -> 1/10 -> 8/10 across three iterations).
- Format compliance and semantic correctness are separate failure modes —
  a schema-matching JSON block can still be substantively wrong. This module
  can only catch the former (see run_verdict_skill's retry-until-valid-json).
  The latter is Phase 3's problem, not this module's.
"""
from __future__ import annotations

import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

from .config import COPILOT_MODEL, DEFAULT_COPILOT_TIMEOUT_SECONDS

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

COPILOT_BIN = shutil.which("copilot") or "copilot"


class CopilotError(Exception):
    """Base for every failure this module raises."""


class CopilotUnavailableError(CopilotError):
    """Equivalent of anthropic.APIConnectionError/RateLimitError — auth
    expired, rate-limited, network down, binary missing, or a rejected
    --model value. Never code-fixable: task_loop must defer (return
    'not_started'), not diagnose-and-retry, and this must NOT consume one of
    the ticket's retry attempts — see Full flow's Jira/GitHub-failure
    handling, which this mirrors for the Copilot backend itself."""


class CopilotInvocationCrashed(CopilotError):
    """The `copilot -p` process died mid-way: non-zero exit not explained by
    a clean rejection, or it was killed after exceeding its timeout. Plan's
    Full flow: force-remove and recreate the worktree so the next attempt
    starts clean, never from a dead invocation's partial edits — and this
    DOES still consume one of the 5 attempts, or a persistently-crashing
    environment retries forever outside the cap."""


class CopilotJsonFormatError(CopilotError):
    """A verdict-shaped skill never produced a parseable, schema-matching
    JSON block even after the retry-until-valid-json fallback. Per the plan,
    this is NOT its own failure kind — it feeds the normal diagnosis-and-
    retry loop exactly like a failing guardrail/test/review would."""

    def __init__(self, message: str, raw_output: str):
        super().__init__(message)
        self.raw_output = raw_output


@dataclass
class CopilotResult:
    stdout: str
    stderr: str
    returncode: int
    duration_seconds: float


# Substrings that mark a failure as "the backend itself is unavailable", not
# "the model did something wrong" — checked case-insensitively against the
# combined stdout+stderr of a non-zero exit. Deliberately conservative: an
# unmatched non-zero exit falls through to CopilotInvocationCrashed, which
# still counts against the retry cap (fails closed, not open).
_UNAVAILABLE_MARKERS = (
    "not authenticated",
    "authentication",
    "401",
    "403",
    "rate limit",
    "429",
    "quota",
    "econnreset",
    "etimedout",
    "enotfound",
    "network",
    "is not available",  # e.g. a rejected --model value (spike #8)
)


def _run_copilot_process(
    cmd: list[str], cwd: Path, timeout: int
) -> tuple[str, str, int, bool]:
    """Runs `cmd`, returns (stdout, stderr, returncode, timed_out). Kills the
    process itself on timeout rather than leaving that to the caller — the
    same mechanism a real hang and a deliberately-short test timeout both
    exercise, so a unit test can prove the crash-and-clean path without
    needing to externally race-kill a process (see Phase 1's proof)."""
    proc = subprocess.Popen(
        cmd, cwd=str(cwd), stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, encoding="utf-8", errors="replace",
    )
    try:
        stdout, stderr = proc.communicate(timeout=timeout)
        return stdout, stderr, proc.returncode, False
    except subprocess.TimeoutExpired:
        proc.kill()
        stdout, stderr = proc.communicate()
        return stdout, stderr, -9, True


def run_copilot(
    prompt: str,
    cwd: Path,
    agent: str | None = None,
    allow_tools: list[str] | None = None,
    model: str = COPILOT_MODEL,
    timeout: int = DEFAULT_COPILOT_TIMEOUT_SECONDS,
) -> CopilotResult:
    """One `copilot -p` invocation, scoped to `cwd` (a ticket's worktree, or
    a scratch/smoke-test repo). `allow_tools` — a list like
    ["write", "shell", "shell(pytest:*)"]; pass None or [] to grant NO tool
    access at all (the verdict-shaped skills' required posture, per spike
    #4's finding — see module docstring).

    Raises CopilotUnavailableError (defer, don't count) or
    CopilotInvocationCrashed (force-clean worktree, counts against the cap).
    Returns CopilotResult on a clean (returncode 0) run — callers are still
    responsible for judging whether the *content* is any good; this
    function only judges whether the invocation itself completed.
    """
    cmd = [COPILOT_BIN, "-p", prompt, "--model", model, "--no-ask-user", "-s"]
    if agent:
        cmd += ["--agent", agent]
    for tool in allow_tools or []:
        cmd += ["--allow-tool", tool]

    start = time.monotonic()
    try:
        stdout, stderr, returncode, timed_out = _run_copilot_process(cmd, cwd, timeout)
    except FileNotFoundError as e:
        raise CopilotUnavailableError(f"copilot binary not found ({COPILOT_BIN}): {e}") from e
    duration = time.monotonic() - start

    if timed_out:
        raise CopilotInvocationCrashed(
            f"copilot invocation exceeded {timeout}s and was killed (cwd={cwd})"
        )
    if returncode == 0:
        return CopilotResult(stdout=stdout, stderr=stderr, returncode=0, duration_seconds=duration)

    combined_lower = f"{stdout}\n{stderr}".lower()
    if any(marker in combined_lower for marker in _UNAVAILABLE_MARKERS):
        raise CopilotUnavailableError(
            f"copilot backend unavailable (exit {returncode}): {stderr[-2000:] or stdout[-2000:]}"
        )
    raise CopilotInvocationCrashed(
        f"copilot exited {returncode}: {' '.join(cmd[:4])}...\n"
        f"stdout(tail): {stdout[-2000:]}\nstderr(tail): {stderr[-2000:]}"
    )


_JSON_FENCE_RE = re.compile(r"```json\s*(\{.*?\})\s*```", re.DOTALL)


def _extract_json_block(text: str) -> str | None:
    match = _JSON_FENCE_RE.search(text)
    return match.group(1) if match else None


def run_verdict_skill(
    agent: str,
    message: str,
    schema_required_keys: list[str],
    cwd: Path,
    model: str = COPILOT_MODEL,
    timeout: int = DEFAULT_COPILOT_TIMEOUT_SECONDS,
    max_json_retries: int = 2,
) -> dict:
    """Invokes a verdict-shaped skill (requirement-reviewer / structural-
    reviewer / failure-diagnoser) with NO tool access, and enforces the
    closing-fenced-JSON-block contract with a retry-until-valid-json
    fallback (plan: "JSON-reliability fallback"). `message` must already
    contain the ticket/diff/output text — this skill is told that content is
    complete and authoritative and must not go looking for more (spike #4).

    Raises CopilotJsonFormatError if `max_json_retries` re-prompts still
    don't produce a parseable, schema-matching block — per the plan this is
    NOT a distinct failure kind, callers should feed it into the same
    diagnosis-and-retry path as any other stage failure.

    Does NOT catch "valid JSON but semantically wrong" (e.g. clear_to_merge:
    true on a diff that shouldn't merge) — nothing here can. That's exactly
    the gap Phase 3's human-advisory review period exists to cover
    (confirmed as a real, observed failure mode in spike #4: 7/8
    format-valid runs were still substantively wrong).
    """
    import json

    prompt = message
    last_raw = ""
    for attempt in range(max_json_retries + 1):
        result = run_copilot(prompt, cwd=cwd, agent=agent, allow_tools=[], model=model, timeout=timeout)
        last_raw = result.stdout
        block = _extract_json_block(result.stdout)
        if block is not None:
            try:
                data = json.loads(block)
            except json.JSONDecodeError as e:
                parse_error = f"json.JSONDecodeError: {e}"
            else:
                missing = [k for k in schema_required_keys if k not in data]
                if not missing:
                    return data
                parse_error = f"missing required key(s): {missing}"
        else:
            parse_error = "no fenced ```json block found in the response"

        if attempt < max_json_retries:
            prompt = (
                f"{message}\n\n---\n"
                f"Your previous response did not parse as valid JSON matching the "
                f"required schema (error: {parse_error}). Reproduce your answer as "
                f"valid JSON only, with keys exactly {schema_required_keys}. Your "
                f"ENTIRE response must be ONLY a single fenced ```json block, no "
                f"prose before or after it."
            )

    raise CopilotJsonFormatError(
        f"{agent}: no valid JSON matching {schema_required_keys} after "
        f"{max_json_retries + 1} attempt(s)",
        raw_output=last_raw,
    )
