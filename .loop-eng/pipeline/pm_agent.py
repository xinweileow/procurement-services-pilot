"""PM agent — deterministic next-ticket selection. Dependency ordering is a
solved problem, not a judgment call, so this isn't an LLM call.

Ported verbatim from loopengineering/src/pipeline/pm_agent.py — plan's
Reference implementations marks this "unchanged, reused as-is".
"""
from __future__ import annotations

from .schemas import Backlog, BacklogItem


def pick_next_task(backlog: Backlog) -> BacklogItem | None:
    done = {i.local_id for i in backlog.items if i.loop_status == "done"}
    parent_ids = {i.parent for i in backlog.items if i.parent}
    for item in backlog.items:
        if item.issue_type == "epic" or item.local_id in parent_ids:
            continue
        if item.status != "approved":
            continue
        if item.loop_status == "escalated":
            print(f"[{item.local_id}] escalated — skipping (pass --retry-escalated to retry).")
            continue
        if item.loop_status != "not_started":
            continue
        if not all(dep in done for dep in item.depends_on):
            continue
        if item.intent != "build":
            print(f"[{item.local_id}] intent={item.intent!r} — needs a stakeholder decision, not fed to the developer skill.")
            continue
        return item
    return None
