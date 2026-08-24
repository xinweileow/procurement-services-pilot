"""The actual logic behind `.loop-eng/hooks/pre-push` (plan's folder
structure tree: "hooks/pre-push — independent gate — can't be bypassed
either mode"). Split into a real Python module rather than inlining
everything in the shell shim, so it can be unit-exercised directly instead
of only through a real `git push`.

Independently re-runs the same checks the post-hoc gate runs mid-loop
(guardrails, regression suite), plus a secrets scan over the actual PUSH
RANGE (not the staged index — by push time everything is already committed,
so `git diff --cached` has nothing to show; see guardrails.py's
`scan_diff_text_for_secrets` docstring). This is the gate a human finishing
an escalated ticket by hand goes through too — same checks, same failure
mode, regardless of who or what is pushing (plan's Full flow: "Same
pre-push hook enforces the gate regardless of who's pushing").

KNOWN GAP (pre-flight item 5): this is bypassable via `git push --no-verify`
— it's the LOCAL half of the gate only. A server-side check (CI re-check /
branch protection on the target repo) is still needed as the second line of
defense and is NOT built here.

Reads git's pre-push hook stdin contract: one line per ref being pushed,
`<local ref> SP <local sha1> SP <remote ref> SP <remote sha1>`. A local
sha1 of all zeros means a ref deletion (skipped — nothing to scan). A
remote sha1 of all zeros means a new ref with no prior history (diffed
against git's empty-tree hash instead of a real parent).
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from pipeline import guardrails, test_agent  # noqa: E402

ZERO_SHA = "0" * 40
EMPTY_TREE_SHA = "4b825dc642cb6eb9a060e54bf8d69288fbee4904"


def _diff_range(local_sha: str, remote_sha: str, repo_root: Path) -> str:
    base = EMPTY_TREE_SHA if remote_sha == ZERO_SHA else remote_sha
    result = subprocess.run(
        ["git", "diff", f"{base}..{local_sha}", "--unified=0"],
        cwd=str(repo_root), capture_output=True, text=True, encoding="utf-8",
    )
    return result.stdout


def check_push(stdin_lines: list[str], repo_root: Path) -> tuple[bool, str]:
    """Returns (allowed, message). Runs ruff + the full regression suite
    once (both are about "the code as it stands right now", independent of
    which specific refs are being pushed), plus a secrets scan per ref's
    actual push range."""
    messages: list[str] = []
    ok = True

    guardrail = guardrails.run_guardrails(repo_root)
    if not guardrail.passed:
        ok = False
        messages.append(f"[guardrails/lint] {guardrail.output}")

    for line in stdin_lines:
        line = line.strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) != 4:
            continue
        _local_ref, local_sha, _remote_ref, remote_sha = parts
        if local_sha == ZERO_SHA:
            continue  # ref deletion, nothing to scan
        diff_text = _diff_range(local_sha, remote_sha, repo_root)
        findings = guardrails.scan_diff_text_for_secrets(diff_text)
        if findings:
            ok = False
            messages.append(f"[secrets scan] {local_sha[:8]}: " + "; ".join(findings))

    regression = test_agent.run_regression_suite(repo_root)
    if not regression.passed:
        ok = False
        messages.append(f"[regression suite]\n{regression.output[-4000:]}")

    if ok:
        return True, "pre-push: guardrails + secrets scan + regression suite all passed."
    return False, "pre-push BLOCKED:\n" + "\n\n".join(messages)


def main() -> int:
    repo_root = Path(
        subprocess.run(
            ["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, encoding="utf-8", check=True
        ).stdout.strip()
    )
    if not (repo_root / ".loop-eng" / "pipeline").is_dir():
        print("pre-push: .loop-eng/pipeline not found — skipping gate (not a loop-eng-managed repo?)")
        return 0

    stdin_lines = sys.stdin.readlines()
    allowed, message = check_push(stdin_lines, repo_root)
    print(message)
    return 0 if allowed else 1


if __name__ == "__main__":
    raise SystemExit(main())
