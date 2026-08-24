"""Scope guard, ported to POST-HOC enforcement.

The predecessor's scope_guard.py (loopengineering/src/pipeline/scope_guard.py)
enforced scope INLINE: propose_scope was a forced tool call the Developer
agent had to make before any write_file call, and write_file itself (our own
tool wrapper) rejected paths outside the declared scope. That mechanism
cannot port as-is: Copilot's `--allow-tool write --allow-tool shell` gives it
NATIVE file access that never calls our write_file wrapper, so there's
nothing to intercept in real time.

The cross-ticket collision-detection *concept* still matters just as much
under Copilot, so it moves to post-hoc enforcement instead (plan's Reference
implementations: "add this to Phase 1's independent post-hoc gate"): after a
Copilot invocation finishes and its edits are staged, diff the worktree
against the ticket's declared scope (asked for up front — in the ticket
Description or a first-turn scope declaration, same as before, just no
longer enforced by intercepting a tool call) and flag any touched path
outside it, or already claimed by a sibling ticket in the ownership ledger.
"""
from __future__ import annotations

import fnmatch
import json
from dataclasses import dataclass, field
from pathlib import Path

from .config import MODULE_OWNERSHIP_PATH


@dataclass
class ScopeResult:
    passed: bool
    output: str
    touched: list[str] = field(default_factory=list)


def _load_ledger() -> dict[str, str]:
    if not MODULE_OWNERSHIP_PATH.exists():
        return {}
    return json.loads(MODULE_OWNERSHIP_PATH.read_text(encoding="utf-8"))


def _save_ledger(ledger: dict[str, str]) -> None:
    MODULE_OWNERSHIP_PATH.parent.mkdir(parents=True, exist_ok=True)
    MODULE_OWNERSHIP_PATH.write_text(json.dumps(ledger, indent=2, sort_keys=True), encoding="utf-8")


def _in_scope(path: str, declared_paths: list[str]) -> bool:
    for pattern in declared_paths:
        if path == pattern:
            return True
        if pattern.endswith("/") and path.startswith(pattern):
            return True
        if fnmatch.fnmatch(path, pattern):
            return True
    return False


def check_scope(
    local_id: str,
    declared_paths: list[str],
    touched_paths: list[str],
) -> ScopeResult:
    """Checks the paths a finished Copilot invocation actually touched
    (`touched_paths` — the caller gets this from `target_repo.changed_paths_
    staged` after `stage_all`) against `declared_paths` (the ticket's own
    up-front scope declaration) and the cross-ticket ownership ledger.

    Claims every touched path for `local_id` in the ledger regardless of
    outcome — same as the predecessor's propose() did on the way in, just
    now recorded on the way out. A ticket that fails this check still gets
    its diagnosis-fed retry (same as a guardrail/test failure); the ledger
    claim isn't gated on passing, so a later sibling ticket still sees the
    collision even if this attempt's out-of-scope edit gets corrected next
    attempt.
    """
    violations = [p for p in touched_paths if not _in_scope(p, declared_paths)]

    ledger = _load_ledger()
    collisions = [
        (p, ledger[p]) for p in touched_paths if p in ledger and ledger[p] != local_id
    ]
    for p in touched_paths:
        ledger[p] = local_id
    _save_ledger(ledger)

    if not violations and not collisions:
        return ScopeResult(passed=True, output="scope ok", touched=touched_paths)

    messages = []
    if violations:
        messages.append(f"touched path(s) outside declared scope {declared_paths}: {violations}")
    if collisions:
        messages.append(f"touched path(s) already claimed by another ticket: {collisions}")
    return ScopeResult(passed=False, output="; ".join(messages), touched=touched_paths)
