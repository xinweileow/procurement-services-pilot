"""The independent post-hoc gate: guardrails + tests + scope check +
requirement review + structural review, run regardless of what the
developer skill self-reports. Never trusts Copilot's own "I'm done" — every
stage here is a deterministic re-check or a separately-invoked reviewer
skill with no knowledge of what the developer skill claimed.

Order matches the predecessor's task_loop.py stage order (guardrail -> test
-> regression -> requirement -> review), with the post-hoc scope check
inserted after the test stages (cheap, deterministic, no LLM) and before
either reviewer skill (both cost a real Copilot invocation) — so a scope
violation gets caught before spending an invocation reviewing a diff that's
going to be rejected anyway.

Phase 1 shipped with structural-reviewer.md only (requirement-reviewer.md
wasn't authored yet). Phase 2 authored it — this is that wiring.
"""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Union

from . import copilot_cli, guardrails, scope_guard, target_repo, test_agent
from .schemas import BacklogItem
from .ticket import Ticket

TicketLike = Union[Ticket, BacklogItem]

REQUIREMENT_REVIEWER_SCHEMA = ["clear_met", "gaps"]
STRUCTURAL_REVIEWER_SCHEMA = ["clear_to_merge", "issues"]


@dataclass
class GateResult:
    passed: bool
    stage: str | None  # which stage failed: guardrail/test/regression/scope/review/review-format
    output: str


def run_post_hoc_gate(
    ticket: TicketLike,
    worktree_path: Path,
    requirement_reviewer_agent: str = "requirement-reviewer",
    reviewer_agent: str = "structural-reviewer",
    backend_standard: str | None = None,
    review_timeout: int = 300,
) -> GateResult:
    guardrail = guardrails.run_guardrails(worktree_path)
    if not guardrail.passed:
        return GateResult(False, "guardrail", guardrail.output)

    if ticket.test_path:
        task_test = test_agent.run_task_tests(ticket.test_path, worktree_path)
        if not task_test.passed:
            return GateResult(False, "test", task_test.output)

    regression = test_agent.run_regression_suite(worktree_path)
    if not regression.passed:
        return GateResult(False, "regression", regression.output)

    touched = target_repo.changed_paths_staged(worktree_path)
    scope_result = scope_guard.check_scope(ticket.local_id, ticket.scope, touched)
    if not scope_result.passed:
        return GateResult(False, "scope", scope_result.output)

    diff = target_repo.diff_staged(worktree_path)
    if not diff.strip():
        return GateResult(False, "requirement", "empty diff — nothing to review")

    requirement_message = f"Ticket {ticket.local_id}: {ticket.summary}\n{ticket.description}\n\nDiff:\n{diff}"
    try:
        requirement_verdict = copilot_cli.run_verdict_skill(
            agent=requirement_reviewer_agent,
            message=requirement_message,
            schema_required_keys=REQUIREMENT_REVIEWER_SCHEMA,
            cwd=worktree_path,
            timeout=review_timeout,
        )
    except copilot_cli.CopilotJsonFormatError as e:
        return GateResult(False, "requirement-format", e.raw_output[-4000:])

    if not requirement_verdict.get("clear_met"):
        gaps = requirement_verdict.get("gaps", [])
        return GateResult(False, "requirement", "\n".join(gaps) if gaps else "clear_met: false")

    review_message = f"Ticket {ticket.local_id}: {ticket.summary}\n\nDiff:\n{diff}"
    if backend_standard:
        review_message += (
            f"\n\nThis project's backend standard — flag any deviation as an "
            f"issue:\n{backend_standard}"
        )

    try:
        verdict = copilot_cli.run_verdict_skill(
            agent=reviewer_agent,
            message=review_message,
            schema_required_keys=STRUCTURAL_REVIEWER_SCHEMA,
            cwd=worktree_path,
            timeout=review_timeout,
        )
    except copilot_cli.CopilotJsonFormatError as e:
        return GateResult(False, "review-format", e.raw_output[-4000:])

    if not verdict.get("clear_to_merge"):
        issues = verdict.get("issues", [])
        return GateResult(False, "review", "\n".join(issues) if issues else "clear_to_merge: false")

    return GateResult(True, None, "gate passed: guardrails + tests + scope + requirement + structural review")
