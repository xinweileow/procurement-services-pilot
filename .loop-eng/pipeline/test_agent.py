"""Test agent + regression suite. Purely mechanical — runs pytest in the
target repo's own venv and reports pass/fail + output. Any LLM reasoning
about a failure happens in the developer skill's retry, not here.

Ported from loopengineering/src/pipeline/test_agent.py (plan's Reference
implementations: "unchanged, reused as-is"), with the TARGET_REPO_PATH
default dropped — Phase 1 has no single fixed target repo, every caller
passes target_dir explicitly.
"""
from __future__ import annotations

import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

TEST_TIMEOUT_SECONDS = 180


@dataclass
class TestResult:
    passed: bool
    output: str


def _venv_python(target_dir: Path) -> Path:
    # venv layout differs by platform: Windows uses Scripts\python.exe, POSIX
    # uses bin/python. Getting this wrong means every subprocess.run call
    # below fails to spawn at all, on whichever platform wasn't hardcoded.
    if sys.platform == "win32":
        return target_dir / ".venv" / "Scripts" / "python.exe"
    return target_dir / ".venv" / "bin" / "python"


def ensure_target_venv(target_dir: Path) -> None:
    """Creates the venv if missing, then always (re)installs requirements —
    "the venv directory exists" doesn't mean dependencies actually installed
    successfully last time. pip install is idempotent and fast when
    everything's already satisfied, so this costs little and stays correct
    if requirements.txt ever changes."""
    venv_python = _venv_python(target_dir)
    if not venv_python.exists():
        subprocess.run([sys.executable, "-m", "venv", str(target_dir / ".venv")], check=True, capture_output=True)
    requirements = target_dir / "requirements.txt"
    if not requirements.exists():
        return
    install = subprocess.run(
        [str(venv_python), "-m", "pip", "install", "-q", "-r", str(requirements)],
        capture_output=True, text=True, encoding="utf-8",
    )
    if install.returncode != 0:
        raise RuntimeError(f"Failed to install target repo dependencies:\n{install.stdout}\n{install.stderr}")


def _run_pytest(args: list[str], target_dir: Path) -> TestResult:
    ensure_target_venv(target_dir)
    try:
        result = subprocess.run(
            [str(_venv_python(target_dir)), "-m", "pytest", *args, "-v"],
            cwd=str(target_dir), capture_output=True, text=True, encoding="utf-8",
            timeout=TEST_TIMEOUT_SECONDS,
        )
    except subprocess.TimeoutExpired as e:
        return TestResult(passed=False, output=f"pytest timed out after {TEST_TIMEOUT_SECONDS}s: {e}")
    return TestResult(passed=result.returncode == 0, output=result.stdout + result.stderr)


def run_task_tests(test_path: str, target_dir: Path) -> TestResult:
    return _run_pytest([test_path], target_dir)


def run_regression_suite(target_dir: Path) -> TestResult:
    return _run_pytest(["tests/"], target_dir)
