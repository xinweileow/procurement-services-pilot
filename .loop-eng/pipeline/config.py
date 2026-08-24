"""Pipeline configuration. LOOP_ENG_ROOT is the .loop-eng directory itself,
not the outer repo — this directory is what the plan describes as "cloned/
submoduled into each target repo", so its own state (KPI log, checkpoints,
ownership ledger) has to be anchored to itself, not to whatever repo happens
to contain it right now. Still no single fixed TARGET_REPO_PATH — every
target_repo.py/worktree.py call takes target_dir explicitly, since which
repo is "the" target repo is a per-project setting (Phase 2's project
config), not a pipeline-wide constant.

Phase 2 addition: loads .env/.env.local for Jira credentials
(JIRA_EMAIL/JIRA_API_TOKEN/JIRA_HOST — see jira_sync.py), same pattern as
the predecessor's config.py. Plain assignment, not setdefault: this
pipeline's own .env/.env.local must be authoritative for its own
credentials, not silently lose to a stray ambient env var.
"""
from __future__ import annotations

import os
from pathlib import Path

LOOP_ENG_ROOT = Path(__file__).resolve().parents[1]


def _load_env(path: Path) -> None:
    if not path.exists():
        return
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            k, _, v = line.partition("=")
            os.environ[k.strip()] = v.strip()


_load_env(LOOP_ENG_ROOT / ".env")
_load_env(LOOP_ENG_ROOT / ".env.local")  # gitignored; for local credential overrides

# Raised from the predecessor's 3 (plan: "MAX_LOOP_RETRY_ATTEMPTS 3 -> 5").
MAX_LOOP_RETRY_ATTEMPTS = 5

# Per-invocation ceiling for a single `copilot -p` call. Generous: real
# ticket work includes edit -> test -> fix -> re-test inside one invocation
# (confirmed by Phase 0 spike #1), which can genuinely take minutes.
DEFAULT_COPILOT_TIMEOUT_SECONDS = 600

# Only "auto" is a valid --model value under this account (Phase 0 spike #8) —
# every explicit model name was rejected. Kept as a named constant, not
# hardcoded at each call site, so a future org-seat entitlement change (see
# spike #8's open question) is a one-line fix.
COPILOT_MODEL = "auto"

KPI_LOG_PATH = LOOP_ENG_ROOT / "data" / "loop" / "kpi_log.jsonl"
MODULE_OWNERSHIP_PATH = LOOP_ENG_ROOT / "data" / "loop" / "module_ownership.json"
STATE_DIR = LOOP_ENG_ROOT / ".loop" / "state"
BACKLOG_DIR = LOOP_ENG_ROOT / "data" / "backlog"

# Jira workflow transition id for "Done". Stable for a given Jira workflow;
# override via env var if a project's workflow differs.
JIRA_DONE_TRANSITION_ID = os.environ.get("JIRA_DONE_TRANSITION_ID", "51")
