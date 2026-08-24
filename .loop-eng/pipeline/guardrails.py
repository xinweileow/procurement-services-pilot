"""Deterministic guardrails: ruff lint/format + a secrets scan against the
target repo before the test stage fires. Catches style issues and obvious
credential leaks without consuming any Copilot invocation. If ruff is
unavailable that step is skipped non-fatally; the secrets scan has no
external dependency.

Ported verbatim from loopengineering/src/pipeline/guardrails.py — plan's
Reference implementations marks this "unchanged, reused as-is".
"""
from __future__ import annotations

import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

_SECRET_PATTERNS = [
    ("AWS access key", re.compile(r"AKIA[0-9A-Z]{16}")),
    ("private key header", re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----")),
    ("GitHub token", re.compile(r"ghp_[A-Za-z0-9]{36}")),
    ("OpenAI-style API key", re.compile(r"sk-[A-Za-z0-9]{20,}")),
    ("Slack token", re.compile(r"xox[baprs]-[A-Za-z0-9-]{10,}")),
    (
        "hardcoded secret assignment",
        re.compile(r"(?i)(secret|api[_-]?key|password|token)\s*=\s*['\"][^'\"]{8,}['\"]"),
    ),
]


@dataclass
class GuardrailResult:
    passed: bool
    output: str
    skipped: bool = False


def _ruff_cmd(target_dir: Path) -> list[str] | None:
    # venvs put scripts in Scripts/ with a .exe suffix on Windows, bin/ without
    # one everywhere else — check both layouts so this works for both Windows
    # and Mac/Linux teammates against the same target repo.
    bin_dir = target_dir / ".venv" / ("Scripts" if sys.platform == "win32" else "bin")
    exe_suffix = ".exe" if sys.platform == "win32" else ""

    venv_ruff = bin_dir / f"ruff{exe_suffix}"
    if venv_ruff.exists():
        return [str(venv_ruff)]
    venv_python = bin_dir / f"python{exe_suffix}"
    if venv_python.exists():
        probe = subprocess.run(
            [str(venv_python), "-m", "ruff", "--version"],
            capture_output=True, text=True, encoding="utf-8",
        )
        if probe.returncode == 0:
            return [str(venv_python), "-m", "ruff"]
    try:
        probe = subprocess.run(["ruff", "--version"], capture_output=True, text=True, encoding="utf-8")
    except FileNotFoundError:
        return None
    if probe.returncode == 0:
        return ["ruff"]
    return None


def scan_diff_text_for_secrets(diff_text: str) -> list[str]:
    """Regex-scan added lines of a unified diff for common secret shapes.
    Only checks '+' lines so pre-existing (already-committed) matches in
    untouched code don't fail every run. Takes plain diff text rather than a
    target_dir + git invocation so callers can hand it any diff range —
    the staged index (this module's own run_guardrails), or a push range
    between two commits (hooks/pre_push_check.py), which has nothing staged
    at all by the time a push happens."""
    findings: list[str] = []
    current_file = "?"
    for line in diff_text.splitlines():
        if line.startswith("+++ b/"):
            current_file = line[len("+++ b/"):]
            continue
        if not line.startswith("+") or line.startswith("+++"):
            continue
        added = line[1:]
        for name, pattern in _SECRET_PATTERNS:
            if pattern.search(added):
                findings.append(f"possible {name} in {current_file}: {added.strip()[:120]}")
    return findings


def _scan_staged_diff_for_secrets(target_dir: Path) -> list[str]:
    try:
        r = subprocess.run(
            ["git", "diff", "--cached", "--unified=0"],
            cwd=str(target_dir), capture_output=True, text=True, encoding="utf-8", timeout=30,
        )
    except (subprocess.TimeoutExpired, FileNotFoundError):
        return []
    if r.returncode != 0:
        return []
    return scan_diff_text_for_secrets(r.stdout)


def run_guardrails(target_dir: Path) -> GuardrailResult:
    """Run ruff lint + format check, then a deterministic secrets scan over
    the staged diff. Returns pass/fail + combined output."""
    outputs: list[str] = []
    failed = False

    cmd = _ruff_cmd(target_dir)
    if cmd is None:
        outputs.append("ruff not found — lint guardrail skipped")
    else:
        for step in [["check", "."], ["format", "--check", "."]]:
            try:
                r = subprocess.run(
                    [*cmd, *step], cwd=str(target_dir), capture_output=True, text=True,
                    encoding="utf-8", timeout=60,
                )
            except subprocess.TimeoutExpired:
                outputs.append(f"ruff {step[0]} timed out after 60s")
                failed = True
                continue
            if r.returncode != 0:
                failed = True
            if r.stdout.strip():
                outputs.append(r.stdout.strip())
            if r.stderr.strip():
                outputs.append(r.stderr.strip())

    secret_findings = _scan_staged_diff_for_secrets(target_dir)
    if secret_findings:
        failed = True
        outputs.append("secrets scan:\n" + "\n".join(secret_findings))

    return GuardrailResult(
        passed=not failed,
        output="\n".join(outputs) if outputs else "ruff + secrets scan: no issues",
    )
