"""Lightweight ticket representation for Phase 1's standalone coding agent.

Not the real `schemas.BacklogItem` (loopengineering/src/pipeline/schemas.py)
— that's Jira/backlog-shaped and belongs to Phase 2's wiring. Phase 1 proves
the coding-agent mechanism against one hand-written synthetic ticket, no
Jira/backlog involved, so this only needs the fields the coding-agent loop
itself actually consumes. `description` is the entire prompt for the
developer skill (plan: "the ticket's pre-written Description, precise enough
to be the entire prompt for that ticket's later autonomous run") and `scope`
is the up-front scope declaration the post-hoc scope_guard checks against.
"""
from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class Ticket:
    local_id: str
    summary: str
    description: str
    scope: list[str] = field(default_factory=list)
    test_path: str | None = None
