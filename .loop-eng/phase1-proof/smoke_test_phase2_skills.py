""""How to author a skill file" step 8, for Phase 2's verdict-shaped skills.

Same discipline as smoke_test_skills.py (Phase 1's dev-throwaway.md /
structural-reviewer.md smoke tests): don't wire a skill into task_loop.py
until its authored .md file has been proven against a real invocation.

Covers requirement-reviewer.md and failure-diagnoser.md here — both are
zero-tool-access, single-verdict-JSON skills with the exact same shape as
structural-reviewer.md (already proven in Phase 1), so they reuse
copilot_cli.run_verdict_skill the same way. kb-writer.md and fsd-writer.md
get their own checks (they produce markdown files, not a JSON verdict, so
the pass/fail shape is different) — fsd-writer's test runs kb-writer first
in the same scratch repo, since fsd-writer reads business_kb.md/ui_ux.md
that only kb-writer produces.

spec-writer.md and developer-backend.md/developer-dotnet.md/developer-
frontend.md are NOT covered here — they need real scratch-repo scaffolding
(an actual FastAPI/dotnet/React app skeleton, or an interactive chat
partner for spec-writer) that's a bigger lift than a copy-paste extension
of this pattern. Left as follow-up work once quota is available to iterate
against a real invocation rather than guessing at the right scaffold shape
blind.

BLOCKED: same quota exhaustion as the rest of Phase 1 — see
.loop-eng/phase1-proof/README.md. Written now per the "finish code now,
verify later" direction; not yet executed against live Copilot.

Usage:
    python .loop-eng/phase1-proof/smoke_test_phase2_skills.py
"""
from __future__ import annotations

import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from pipeline import copilot_cli  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

TEMPLATES_DIR = Path(__file__).resolve().parents[1] / "agent-templates"
VERDICT_SKILLS = ("requirement-reviewer", "failure-diagnoser", "kb-writer", "fsd-writer")


def _git(args: list[str], cwd: Path, check: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *args], cwd=str(cwd), capture_output=True, text=True, encoding="utf-8", check=check
    )


def _scaffold_scratch_repo() -> Path:
    scratch = Path(tempfile.mkdtemp(prefix="loop-eng-smoke-p2-"))
    agents_dir = scratch / ".github" / "agents"
    agents_dir.mkdir(parents=True)
    for name in VERDICT_SKILLS:
        shutil.copy(TEMPLATES_DIR / f"{name}.md", agents_dir / f"{name}.md")
    _git(["init", "-q"], scratch)
    _git(["config", "user.email", "smoke@local"], scratch)
    _git(["config", "user.name", "smoke"], scratch)
    return scratch


def smoke_test_requirement_reviewer(scratch: Path) -> bool:
    print("\n=== smoke test: requirement-reviewer.md — real invocation against a real `git diff` ===")
    (scratch / "greeter.py").write_text('def greet(name: str) -> str:\n    return f"Hello, {name}!"\n', encoding="utf-8")
    _git(["add", "-A"], scratch)
    diff = _git(["diff", "--cached"], scratch).stdout
    if not diff.strip():
        print("FAIL: expected a non-empty real git diff to review, got nothing.")
        return False

    message = (
        "Ticket SMOKE-3: add a greet(name) helper that returns 'Hello, <name>!'.\n\n"
        f"Diff:\n{diff}"
    )
    try:
        verdict = copilot_cli.run_verdict_skill(
            agent="requirement-reviewer", message=message,
            schema_required_keys=["clear_met", "gaps"], cwd=scratch, timeout=180,
        )
    except copilot_cli.CopilotJsonFormatError as e:
        print(f"FAIL: never produced valid JSON: {e}\nraw tail: {e.raw_output[-1500:]}")
        return False

    print(f"verdict: {verdict}")
    if "clear_met" in verdict and isinstance(verdict["gaps"], list):
        print(f"{'PASS' if verdict['clear_met'] else 'PARTIAL'}: format-valid; "
              f"clear_met={verdict['clear_met']} (semantic correctness needs a manual look either way).")
        return True
    print(f"FAIL: schema mismatch: {verdict!r}")
    return False


def smoke_test_failure_diagnoser(scratch: Path) -> bool:
    print("\n=== smoke test: failure-diagnoser.md — real invocation against a real failure ===")
    message = (
        "Ticket SMOKE-4: add subtract(a, b) to calc.py.\n\n"
        "Failed stage: test (attempt 1)\n\n"
        "Stage output:\n"
        "FAILED test_calc.py::test_subtract - assert 8 == 2\n"
        "  where 8 = subtract(5, 3)\n\n"
        "Diff:\n"
        "diff --git a/calc.py b/calc.py\n"
        "+def subtract(a, b):\n"
        "+    return a + b  # BUG: should be a - b\n"
    )
    try:
        diagnosis = copilot_cli.run_verdict_skill(
            agent="failure-diagnoser", message=message,
            schema_required_keys=["root_cause", "fix_direction", "likely_fixable"],
            cwd=scratch, timeout=180,
        )
    except copilot_cli.CopilotJsonFormatError as e:
        print(f"FAIL: never produced valid JSON: {e}\nraw tail: {e.raw_output[-1500:]}")
        return False

    print(f"diagnosis: {diagnosis}")
    if all(k in diagnosis for k in ("root_cause", "fix_direction", "likely_fixable")):
        print("PASS: format-valid (semantic correctness — does it actually spot the +/- bug? — needs a manual look).")
        return True
    print(f"FAIL: schema mismatch: {diagnosis!r}")
    return False


def smoke_test_kb_writer(scratch: Path) -> bool:
    print("\n=== smoke test: kb-writer.md — real invocation against real source docs ===")
    source = (
        "# Widget Ordering — Business Requirements\n\n"
        "Customers can browse widgets and place an order. Orders are approved by a "
        "manager before fulfillment.\n\n"
        "Open TBD: it is not yet decided whether partial order fulfillment is allowed.\n"
    )
    prompt = (
        "Index this source document into the three KB files.\n\n"
        f"# SOURCE: requirements.md\n{source}"
    )
    try:
        result = copilot_cli.run_copilot(
            prompt, cwd=scratch, agent="kb-writer",
            allow_tools=["write", "shell(git:*)"], timeout=180,
        )
    except copilot_cli.CopilotError as e:
        print(f"FAIL: invocation error: {e}")
        return False

    expected = [scratch / "docs" / "kb" / n for n in ("business_kb.md", "technical_kb.md", "ui_ux.md")]
    missing = [p for p in expected if not p.exists()]
    print(f"copilot exit 0 in {result.duration_seconds:.1f}s")
    if missing:
        print(f"FAIL: missing expected KB file(s): {missing}")
        return False

    biz = (scratch / "docs" / "kb" / "business_kb.md").read_text(encoding="utf-8")
    if "## Open TBDs" not in biz or "partial order fulfillment" not in biz.lower():
        print("FAIL: business_kb.md missing required header or dropped the Open TBD item.")
        return False
    if "## Capabilities" not in biz or "### Feature:" not in biz:
        print("FAIL: business_kb.md missing the Capabilities/Feature-block shape (single-module case).")
        return False
    if "Requirements Checklist" not in biz:
        print("FAIL: business_kb.md's Feature block is missing a Requirements Checklist.")
        return False

    print("PASS: kb-writer.md wrote all three files; business_kb.md kept the Open TBD item and used the Feature-block shape.")
    return True


def smoke_test_fsd_writer(scratch: Path) -> bool:
    print("\n=== smoke test: fsd-writer.md — real invocation enriching a real kb-writer sketch ===")
    if not (scratch / "docs" / "kb" / "business_kb.md").exists():
        print("FAIL: business_kb.md must already exist (run smoke_test_kb_writer first in the same scratch repo).")
        return False

    biz = (scratch / "docs" / "kb" / "business_kb.md").read_text(encoding="utf-8")
    feature_names = re.findall(r"^### Feature: (.+)$", biz, flags=re.MULTILINE)
    if not feature_names:
        print("FAIL: no '### Feature: <name>' found in business_kb.md — can't verify fsd-writer traces it.")
        return False

    prompt = (
        "Read docs/kb/business_kb.md and docs/kb/ui_ux.md in this repository and enrich "
        "docs/kb/technical_kb.md per your instructions."
    )
    try:
        result = copilot_cli.run_copilot(
            prompt, cwd=scratch, agent="fsd-writer",
            allow_tools=["write", "shell(git:*)"], timeout=180,
        )
    except copilot_cli.CopilotError as e:
        print(f"FAIL: invocation error: {e}")
        return False

    print(f"copilot exit 0 in {result.duration_seconds:.1f}s")
    tech_path = scratch / "docs" / "kb" / "technical_kb.md"
    if not tech_path.exists():
        print("FAIL: technical_kb.md missing after fsd-writer ran.")
        return False

    tech = tech_path.read_text(encoding="utf-8")
    if "## Feature Trace" not in tech:
        print("FAIL: technical_kb.md missing the '## Feature Trace' section.")
        return False
    if not any(name in tech for name in feature_names):
        print(f"FAIL: none of business_kb.md's Feature name(s) {feature_names} appear in technical_kb.md's Feature Trace.")
        return False
    if "## REST API Listing" not in tech:
        print("FAIL: technical_kb.md missing the '## REST API Listing' section.")
        return False

    print(f"PASS: fsd-writer.md enriched technical_kb.md with a Feature Trace referencing {feature_names} and a REST API Listing.")
    return True


def main() -> int:
    scratch = _scaffold_scratch_repo()
    print(f"scratch repo: {scratch}")
    results = {}
    try:
        results["requirement-reviewer"] = smoke_test_requirement_reviewer(scratch)
        results["failure-diagnoser"] = smoke_test_failure_diagnoser(scratch)
        results["kb-writer"] = smoke_test_kb_writer(scratch)
        results["fsd-writer"] = smoke_test_fsd_writer(scratch) if results["kb-writer"] else False
    finally:
        shutil.rmtree(scratch, ignore_errors=True)

    print("\n=== summary ===")
    ok = True
    for name, passed in results.items():
        print(f"  {'PASS' if passed else 'FAIL'}: {name}")
        ok = ok and passed
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
