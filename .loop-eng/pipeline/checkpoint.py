"""Live checkpoint file for an escalated ticket — `.loop/state/<ticket-id>.json`
(plan's folder structure tree). Written once a ticket exhausts its retry
attempts, so a human's later escalation session (Copilot Chat, per Full flow)
has the same developer skill + same ticket Description + last diagnosis
already loaded instead of starting cold. Shape borrows gstack's
context-save/context-restore markdown+YAML checkpoint idea (plan's Reference
implementations), simplified to flat JSON since this is machine-read by the
escalation flow, not hand-edited.
"""
from __future__ import annotations

import json
from dataclasses import asdict, is_dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .config import STATE_DIR
from .gate import TicketLike


def _ticket_to_dict(ticket: TicketLike) -> dict:
    # Phase 1's Ticket is a plain dataclass; Phase 2's real BacklogItem is a
    # pydantic model — checkpoints need to serialize either one a caller
    # might pass in.
    if is_dataclass(ticket):
        return asdict(ticket)
    return ticket.model_dump()


def write_checkpoint(
    ticket: TicketLike,
    stage: str,
    attempts: dict[str, int],
    history: list[dict[str, Any]],
    last_feedback: str | None,
) -> Path:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    path = STATE_DIR / f"{ticket.local_id}.json"
    payload = {
        "ticket": _ticket_to_dict(ticket),
        "stalled_at_stage": stage,
        "attempts": attempts,
        "history": history,
        "last_feedback": last_feedback,
        "written_at": datetime.now(timezone.utc).isoformat(),
    }
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return path


def read_checkpoint(local_id: str) -> dict[str, Any] | None:
    path = STATE_DIR / f"{local_id}.json"
    if not path.exists():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def clear_checkpoint(local_id: str) -> None:
    path = STATE_DIR / f"{local_id}.json"
    if path.exists():
        path.unlink()
