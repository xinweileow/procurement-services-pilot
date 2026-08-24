"""Classifies a ticket into a TaskProfile (execution tier) and selects which
developer-*.md skill applies to it by declared tech stack. Purely
deterministic — no LLM involved on either count.

Tier classification ported from loopengineering/src/pipeline/task_router.py,
adapted: the predecessor picked a MODEL_CHORE/MODEL_FEATURE/MODEL_HOTFIX
Anthropic model per tier — no equivalent exists under Copilot (Phase 0
spike #8: only `--model auto` works under this account), so `model` is
dropped from TaskProfile entirely rather than kept as a dead field. The
hotfix N-sandbox-race pattern the tier still flags is not wired into
task_loop.py in this pass — flagged, not built, same as the plan's own
Reference implementations section leaves it (the plan never asked for it).

Stack-based skill selection (select_developer_skill) is NEW — the
predecessor has no equivalent (single-stack FastAPI/Python pipeline, no
routing needed). Required because Copilot never infers its own routing
(plan: "task_router.py picks exactly ONE developer-*.md skill per ticket by
tech stack ... Copilot never infers its own routing") and
`.loop-eng/agent-templates/spec-writer.md` (Rule 7) now declares each
ticket's stack as the first `technical_constraints` entry specifically so
this function has something deterministic to key off.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

from .schemas import BacklogItem

Tier = Literal["chore", "feature", "hotfix"]

_HOTFIX_KEYWORDS = frozenset({"hotfix", "urgent", "critical", "p0", "incident"})

_STACK_TO_AGENT = {
    "backend": "developer-backend",
    "dotnet": "developer-dotnet",
    "frontend": "developer-frontend",
}
DEFAULT_DEVELOPER_AGENT = "developer-backend"


@dataclass
class TaskProfile:
    tier: Tier
    parallel_limit: int  # >1 would trigger a sandbox-race pattern (hotfix) -- not wired into task_loop.py yet


def classify(ticket: BacklogItem) -> TaskProfile:
    text = (ticket.summary + " " + ticket.description).lower()
    if any(kw in text for kw in _HOTFIX_KEYWORDS):
        return TaskProfile(tier="hotfix", parallel_limit=3)
    if ticket.issue_type == "task" and not ticket.depends_on:
        return TaskProfile(tier="chore", parallel_limit=1)
    return TaskProfile(tier="feature", parallel_limit=1)


def select_developer_skill(ticket: BacklogItem) -> str:
    """Parses the `stack: <backend|dotnet|frontend>` convention
    spec-writer.md fixes as the first technical_constraints entry. Falls
    back to DEFAULT_DEVELOPER_AGENT with a loud warning rather than silently
    guessing wrong, if a ticket has no stack declared (e.g. hand-authored
    outside spec-writer.md, like Phase 1's synthetic ticket)."""
    for constraint in ticket.technical_constraints:
        key, sep, value = constraint.partition(":")
        if sep and key.strip().lower() == "stack":
            stack = value.strip().lower()
            agent = _STACK_TO_AGENT.get(stack)
            if agent:
                return agent
            print(f"[{ticket.local_id}] unrecognized stack {value.strip()!r} in technical_constraints "
                  f"— falling back to {DEFAULT_DEVELOPER_AGENT}.")
            return DEFAULT_DEVELOPER_AGENT
    print(f"[{ticket.local_id}] no 'stack: ...' entry in technical_constraints "
          f"— falling back to {DEFAULT_DEVELOPER_AGENT}. Was this ticket drafted by spec-writer.md?")
    return DEFAULT_DEVELOPER_AGENT
