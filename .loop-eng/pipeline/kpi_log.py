"""KPI log — append-only JSONL, one line per ticket outcome from a task loop
run. Ported from loopengineering/src/pipeline/kpi_log.py, extended per the
plan's Critical files entry ("+ stage_meta") with a `backend` field (always
"copilot" in this restructure — Phase 3 needs this column to exist from day
one for its verdict-vs-human-decision tracking) and a `duration_seconds`
field per stage.
"""
from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any

from .config import KPI_LOG_PATH


def log_task_outcome(
    local_id: str,
    outcome: str,
    attempts: dict[str, int],
    pr_url: str | None = None,
    notes: str | None = None,
    duration_seconds: float | None = None,
    backend: str = "copilot",
) -> None:
    """outcome: one of 'done', 'in_progress', 'escalated', 'not_started'.
    attempts: e.g. {'developer': 2, 'guardrail': 2, 'test': 2, 'scope': 2,
    'review': 1, 'crash': 0, 'human_review': 1}."""
    entry: dict[str, Any] = {
        "local_id": local_id,
        "outcome": outcome,
        "attempts": attempts,
        "pr_url": pr_url,
        "notes": notes,
        "backend": backend,
        "duration_seconds": duration_seconds,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
    KPI_LOG_PATH.parent.mkdir(parents=True, exist_ok=True)
    with KPI_LOG_PATH.open("a", encoding="utf-8") as f:
        f.write(json.dumps(entry) + "\n")


def get_last_escalation_notes(local_id: str) -> str | None:
    """Returns the `notes` field of the most recent 'escalated' outcome
    logged for local_id, so a retry can seed its first attempt with the
    diagnosis already worked out instead of starting cold. Returns None if
    the log doesn't exist yet or has no matching entry."""
    if not KPI_LOG_PATH.exists():
        return None
    notes: str | None = None
    for line in KPI_LOG_PATH.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        entry = json.loads(line)
        if entry.get("local_id") == local_id and entry.get("outcome") == "escalated":
            notes = entry.get("notes")
    return notes
