"""Ticket/backlog data schemas. Ported from
loopengineering/src/pipeline/schemas.py, extended with two fields
`spec-writer.md` (`.loop-eng/agent-templates/spec-writer.md`, Rules 7 and 9)
already produces but the predecessor's schema has no field for:

- `scope: list[str]` — the post-hoc scope declaration (file paths/prefixes/
  globs) `scope_guard.py` checks a finished attempt's touched files
  against. The predecessor never needed this field because its ScopeGuard
  enforcement was inline, keyed off a `propose_scope` tool call at
  *implementation* time — see scope_guard.py's own docstring for why that
  doesn't port as-is under Copilot's native file access. Backlog-drafting
  time is the only "up front" point left for this declaration, so
  spec-writer.md puts it here.
- `technical_constraints: list[str]` — free-form list whose first entry is
  the `stack: backend`/`dotnet`/`frontend` convention spec-writer.md fixes
  (Rule 7), for task_router.py's stack-based skill routing to key off.

BacklogItem satisfies the same duck-typed interface task_loop.py's
run_one_ticket already expects from Phase 1's lightweight `Ticket` dataclass
(`local_id`, `summary`, `description`, `scope`) — a Jira-sourced ticket can
be handed to run_one_ticket directly, no adapter needed.
"""
from __future__ import annotations

from typing import Literal, Optional

from pydantic import BaseModel, Field

MAX_CLARIFICATION_ROUNDS = 3

IssueType = Literal["epic", "story", "task", "subtask"]
TicketStatus = Literal["draft", "approved", "flagged", "needs_followup"]
RejectReason = Literal["wrong_scope", "missing_dependency", "wrong_priority", "other"]
LoopStatus = Literal["not_started", "in_progress", "blocked", "escalated", "done"]
IssueIntent = Literal["decision", "build", "unknown"]


class BacklogItem(BaseModel):
    local_id: str
    project_key: str
    issue_type: IssueType
    summary: str
    description: str
    depends_on: list[str] = Field(default_factory=list)
    blocks: list[str] = Field(default_factory=list)
    parent: str | None = None
    status: TicketStatus = "draft"
    intent: IssueIntent = "unknown"
    loop_status: LoopStatus = "not_started"
    jira_key: str | None = None
    scope: list[str] = Field(default_factory=list)
    technical_constraints: list[str] = Field(default_factory=list)

    @property
    def test_path(self) -> str | None:
        # gate.py checks ticket.test_path optionally -- Jira-sourced tickets
        # have no single canonical acceptance-test file the way Phase 1's
        # hand-written synthetic ticket did (real tickets are judged by
        # requirement-reviewer.md against the Description's Acceptance
        # Criteria, not one fixed pytest path); always None here so gate.py
        # falls through to "just run the full regression suite."
        return None


class Backlog(BaseModel):
    items: list[BacklogItem]

    def by_id(self, local_id: str) -> Optional[BacklogItem]:
        return next((i for i in self.items if i.local_id == local_id), None)


class ClarificationAnswer(BaseModel):
    type: Literal["answer"]
    text: str


class ClarificationDefer(BaseModel):
    type: Literal["defer"]
    reason: str


class RejectFeedback(BaseModel):
    ticket_id: str
    issue_type: RejectReason
    comment: str
