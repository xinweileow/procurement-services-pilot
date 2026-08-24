"""The one hand-written synthetic ticket for Phase 1 step 6's end-to-end
proof. Description follows the real organise_agent.py's Goal/Reference/
Scope/Acceptance Criteria template (plan's Skills table) even though Phase 1
has no spec-writer.md yet — the point is that this Description alone must be
a complete, precise prompt for the developer skill, same discipline the real
pipeline requires of every ticket.
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pipeline.ticket import Ticket  # noqa: E402

DESCRIPTION = """Goal: add a discount_price(price, percent) helper to app/calc.py.

Reference: app/calc.py already has add(a, b) and subtract(a, b). Follow the
same style (plain functions, type hints, no classes).

Scope: app/calc.py, tests/test_discount.py.

Acceptance Criteria:
- discount_price(price: float, percent: float) -> float returns price
  reduced by percent (e.g. discount_price(100, 20) == 80).
- Raises ValueError if percent is not between 0 and 100 inclusive.
- tests/test_discount.py has at least one passing test for the normal case
  and one for the ValueError case.
"""

SYNTHETIC_TICKET = Ticket(
    local_id="PROOF-1",
    summary="Add discount_price() helper",
    description=DESCRIPTION,
    scope=["app/calc.py", "tests/test_discount.py"],
    test_path="tests/test_discount.py",
)
