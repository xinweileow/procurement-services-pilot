"""Quota-independent proof of Phase 2's Jira/backlog wiring: jira_sync.py,
pm_agent.py, task_router.py, and task_loop.run_backlog. No real Jira
credentials or live Copilot involved — `urllib.request.urlopen` is mocked at
the network boundary (not jira_sync's own functions), so `_to_backlog_item`,
`_request`'s error classification, and every real code path in between are
exercised for real. Same "verify what's verifiable without live external
dependencies" discipline as phase1-proof/dry_run_proof.py.

Seven checks:
  1. A realistic Jira issue JSON maps to a BacklogItem correctly (ADF
     description flattening, intent-from-labels, loop_status-from-labels).
  2. pm_agent.pick_next_task's ordering rules (skip epics/parents/escalated/
     unapproved/unmet-deps/non-build) against a small mixed backlog.
  3. task_router.select_developer_skill parses the `stack: ...` convention
     from technical_constraints, with a documented fallback when absent.
  4. jira_sync's actionable-error path: a mocked 401 response raises
     JiraAPIError with a `.detail` that actually names the problem and the
     fix (not a bare "HTTP 401").
  5. task_loop.run_backlog end-to-end: fetch (mocked Jira) -> pick -> route
     -> run_one_ticket (mocked Copilot, same schema-aware fake as
     dry_run_proof.py) -> mark done (mocked Jira) -- AND the Jira-failure
     variant: fetch itself fails -> run_backlog returns {} instead of
     crashing or treating it as a ticket-level failure.
  6. jira_sync.push_backlog_to_jira (copilot.md Kickoff Flow step 4, driven
     via python -m pipeline.main push-backlog): a mocked Jira issue-create/link server (urlopen
     mocked, real create_issue/link_depends_on/_request code exercised)
     proves epic->task->subtask parenting and a depends_on link are wired
     correctly, a draft ticket is never pushed, and a second run against the
     same file creates zero new issues or links (idempotent resume).
  7. task_loop.retry_escalated_ticket (copilot.md Escalation Flow option B,
     driven via python -m pipeline.main retry-escalated): no checkpoint -> defers
     without touching Jira; success -> run_one_ticket is called with exactly
     the resume semantics this path promises (max_attempts=1, the
     checkpoint's diagnosis seeded in, worktree resumed) and Jira/checkpoint
     are synced; failure -> Jira and the checkpoint are left untouched.

Usage:
    python .loop-eng/phase2-proof/test_jira_wiring.py
"""
from __future__ import annotations

import io
import json
import shutil
import sys
import tempfile
import urllib.error
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "phase1-proof"))

from pipeline import checkpoint, copilot_cli, jira_sync, pm_agent, schemas, scope_guard, task_loop, task_router  # noqa: E402
from pr_stub import LocalPRStub  # noqa: E402
from scratch_repo import build_scratch_repo  # noqa: E402

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")


REAL_JIRA_ISSUE = {
    "key": "SCRUM-42",
    "fields": {
        "summary": "Add discount_price() helper",
        "description": {
            "type": "doc", "version": 1,
            "content": [
                {"type": "paragraph", "content": [{"type": "text", "text": "Goal: add a discount helper."}]},
                {"type": "paragraph", "content": [{"type": "text", "text": " Scope: app/calc.py."}]},
            ],
        },
        "issuetype": {"name": "Subtask"},
        "parent": {"key": "SCRUM-40"},
        "status": {"name": "In Progress", "statusCategory": {"key": "indeterminate"}},
        "labels": ["intent-build"],
    },
}


def check_to_backlog_item() -> bool:
    print("\n=== check 1: _to_backlog_item maps a real Jira issue shape correctly ===")
    item = jira_sync._to_backlog_item(REAL_JIRA_ISSUE)
    ok = (
        item.local_id == "SCRUM-42"
        and item.project_key == "SCRUM"
        and item.issue_type == "task"  # subtask -> task per _ISSUE_TYPE_MAP
        and item.parent == "SCRUM-40"
        and item.intent == "build"
        and item.status == "approved"  # not done
        and item.loop_status == "not_started"  # no loop-escalated label
        and "Goal: add a discount helper." in item.description
        and "Scope: app/calc.py." in item.description
    )
    print(f"mapped: local_id={item.local_id!r} issue_type={item.issue_type!r} parent={item.parent!r} "
          f"intent={item.intent!r} description={item.description!r}")
    print("PASS" if ok else "FAIL")
    return ok


def check_pm_agent_ordering() -> bool:
    print("\n=== check 2: pm_agent.pick_next_task ordering rules ===")
    backlog = schemas.Backlog(items=[
        schemas.BacklogItem(local_id="E1", project_key="P", issue_type="epic", summary="epic", description="", status="approved", intent="build"),
        schemas.BacklogItem(local_id="T1", project_key="P", issue_type="task", summary="not approved", description="", status="draft", intent="build"),
        schemas.BacklogItem(local_id="T2", project_key="P", issue_type="task", summary="escalated", description="", status="approved", intent="build", loop_status="escalated"),
        schemas.BacklogItem(local_id="T3", project_key="P", issue_type="task", summary="needs a decision", description="", status="approved", intent="decision"),
        schemas.BacklogItem(local_id="T4", project_key="P", issue_type="task", summary="blocked on T5", description="", status="approved", intent="build", depends_on=["T5"]),
        schemas.BacklogItem(local_id="T5", project_key="P", issue_type="task", summary="the real next task", description="", status="approved", intent="build"),
    ])
    picked = pm_agent.pick_next_task(backlog)
    ok = picked is not None and picked.local_id == "T5"
    print(f"picked: {picked.local_id if picked else None} (expected T5)")
    print("PASS" if ok else "FAIL")
    return ok


def check_task_router_stack_selection() -> bool:
    print("\n=== check 3: task_router.select_developer_skill ===")
    backend = schemas.BacklogItem(local_id="B1", project_key="P", issue_type="task", summary="s", description="", technical_constraints=["stack: backend", "other: x"])
    dotnet = schemas.BacklogItem(local_id="D1", project_key="P", issue_type="task", summary="s", description="", technical_constraints=["stack: dotnet"])
    frontend = schemas.BacklogItem(local_id="F1", project_key="P", issue_type="task", summary="s", description="", technical_constraints=["stack: frontend"])
    unspecified = schemas.BacklogItem(local_id="U1", project_key="P", issue_type="task", summary="s", description="")

    results = {
        "backend": task_router.select_developer_skill(backend) == "developer-backend",
        "dotnet": task_router.select_developer_skill(dotnet) == "developer-dotnet",
        "frontend": task_router.select_developer_skill(frontend) == "developer-frontend",
        "unspecified falls back": task_router.select_developer_skill(unspecified) == task_router.DEFAULT_DEVELOPER_AGENT,
    }
    for name, ok in results.items():
        print(f"  {'PASS' if ok else 'FAIL'}: {name}")
    return all(results.values())


def check_jira_actionable_error() -> bool:
    print("\n=== check 4: jira_sync actionable-error surfacing (mocked 401) ===")
    with mock.patch.dict("os.environ", {"JIRA_EMAIL": "x@example.com", "JIRA_API_TOKEN": "tok", "JIRA_HOST": "example.atlassian.net"}):
        def fake_urlopen(req, timeout=30):
            raise urllib.error.HTTPError(req.full_url, 401, "Unauthorized", {}, io.BytesIO(b"token expired"))

        with mock.patch("urllib.request.urlopen", side_effect=fake_urlopen):
            try:
                jira_sync.fetch_tickets_from_jira("project = X")
            except jira_sync.JiraAPIError as e:
                print(f"detail: {e.detail}")
                ok = "401" in e.detail and "id.atlassian.com" in e.detail and "renew" in e.detail.lower()
                print("PASS" if ok else "FAIL: detail isn't actionable enough")
                return ok
    print("FAIL: expected JiraAPIError, none raised")
    return False


def _fake_verdict(*args, **kwargs) -> dict:
    schema = kwargs.get("schema_required_keys") or (args[2] if len(args) > 2 else [])
    if "clear_met" in schema:
        return {"clear_met": True, "gaps": []}
    return {"clear_to_merge": True, "issues": []}


def check_run_backlog_happy_path() -> bool:
    print("\n=== check 5a: task_loop.run_backlog end-to-end (mocked Jira + mocked Copilot) ===")
    root = Path(tempfile.mkdtemp(prefix="loop-eng-jira-wiring-"))
    try:
        target_dir, _bare = build_scratch_repo(root)
        ticket = schemas.BacklogItem(
            local_id="SCRUM-99", project_key="SCRUM", issue_type="task",
            summary="Add discount_price() helper",
            description="Goal: add discount_price to app/calc.py.\nAcceptance Criteria:\n- it exists.",
            status="approved", intent="build",
            scope=["app/calc.py", "tests/test_discount.py"],
            technical_constraints=["stack: backend"],
        )
        backlog = schemas.Backlog(items=[ticket])

        def fake_run_copilot(prompt, cwd, agent=None, allow_tools=None, model="auto", timeout=600):
            (cwd / "app" / "calc.py").write_text(
                "def add(a, b):\n    return a + b\n\n\n"
                "def subtract(a, b):\n    return a - b\n\n\n"
                "def discount_price(price, percent):\n"
                "    if not (0 <= percent <= 100):\n"
                '        raise ValueError("bad percent")\n'
                "    return price * (1 - percent / 100)\n",
                encoding="utf-8",
            )
            (cwd / "tests" / "test_discount.py").write_text(
                "from app.calc import discount_price\n\n\n"
                "def test_discount():\n    assert discount_price(100, 20) == 80\n",
                encoding="utf-8",
            )
            return copilot_cli.CopilotResult(stdout="ok", stderr="", returncode=0, duration_seconds=0.01)

        marked_done = []
        stub = LocalPRStub(review_sequence=["APPROVED"])
        # Isolated scope ledger: the real .loop-eng/data/loop/module_ownership.json
        # is shared, real, persistent state across every proof script ever
        # run against this repo -- reusing "app/calc.py" (a path Phase 1's
        # own dry_run_proof.py also uses, under a different local_id) would
        # otherwise spuriously collide against leftover claims from a
        # previous, unrelated test run.
        isolated_ledger = root / "module_ownership.json"

        with mock.patch.object(jira_sync, "fetch_tickets_from_jira", return_value=backlog), \
             mock.patch.object(jira_sync, "mark_ticket_done", side_effect=lambda key: marked_done.append(key)), \
             mock.patch.object(copilot_cli, "run_copilot", side_effect=fake_run_copilot), \
             mock.patch.object(copilot_cli, "run_verdict_skill", side_effect=_fake_verdict), \
             mock.patch.object(scope_guard, "MODULE_OWNERSHIP_PATH", isolated_ledger):
            outcomes = task_loop.run_backlog(
                target_dir, confirm=lambda _: True,
                push_and_create_pr=stub.push_and_create_pr,
                check_pr_merged=stub.check_pr_merged,
                get_pr_review_state=stub.get_pr_review_state,
            )

        print(f"outcomes: {outcomes}")
        print(f"marked_done: {marked_done}")
        ok = outcomes.get("SCRUM-99") == "done" and marked_done == ["SCRUM-99"]
        print("PASS" if ok else "FAIL")
        return ok
    finally:
        shutil.rmtree(root, ignore_errors=True)


def check_run_backlog_jira_fetch_failure() -> bool:
    print("\n=== check 5b: task_loop.run_backlog when the initial Jira fetch itself fails ===")
    root = Path(tempfile.mkdtemp(prefix="loop-eng-jira-wiring-fail-"))
    try:
        target_dir, _bare = build_scratch_repo(root)
        with mock.patch.object(
            jira_sync, "fetch_tickets_from_jira",
            side_effect=jira_sync.JiraAPIError("boom", detail="Jira API call failed: simulated for this test."),
        ):
            outcomes = task_loop.run_backlog(target_dir)
        ok = outcomes == {}
        print(f"outcomes: {outcomes} (expected {{}}, no crash)")
        print("PASS" if ok else "FAIL")
        return ok
    finally:
        shutil.rmtree(root, ignore_errors=True)


class _FakeJiraResponse:
    """Minimal stand-in for the `with urlopen(...) as resp:` context manager
    _request() uses, carrying a JSON (or empty) body."""

    def __init__(self, data):
        self._data = json.dumps(data).encode("utf-8") if data is not None else b""

    def read(self):
        return self._data

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False


class _FakeJiraServer:
    """Handles POST /rest/api/3/issue and /rest/api/3/issueLink only --
    exactly what push_backlog_to_jira needs -- assigning incrementing keys
    and recording every issue's fields and every link, so the test can
    assert on parenting and linking without a real Jira instance."""

    def __init__(self, project_key: str):
        self.project_key = project_key
        self._next = 100
        self.issues: dict[str, dict] = {}
        self.links: list[tuple[str, str]] = []
        self.calls: list[str] = []

    def handle(self, req, timeout=30):
        path = req.full_url
        self.calls.append(path)
        body = json.loads(req.data) if req.data else None
        if path.endswith("/rest/api/3/issue"):
            self._next += 1
            key = f"{self.project_key}-{self._next}"
            self.issues[key] = body["fields"]
            return _FakeJiraResponse({"key": key})
        if path.endswith("/rest/api/3/issueLink"):
            self.links.append((body["outwardIssue"]["key"], body["inwardIssue"]["key"]))
            return _FakeJiraResponse(None)
        raise AssertionError(f"unexpected request in push_backlog_to_jira test: {req.get_method()} {path}")


def check_push_backlog_to_jira() -> bool:
    print("\n=== check 6: jira_sync.push_backlog_to_jira (mocked Jira issue-create/link server) ===")
    root = Path(tempfile.mkdtemp(prefix="loop-eng-push-backlog-"))
    try:
        backlog_path = root / "SCRUM-backlog.json"
        backlog = [
            {"local_id": "E1", "project_key": "SCRUM", "issue_type": "epic", "summary": "Epic one",
             "description": "Goal: epic.", "depends_on": [], "blocks": [], "parent": None,
             "status": "approved", "intent": "build", "technical_constraints": []},
            {"local_id": "T1", "project_key": "SCRUM", "issue_type": "task", "summary": "Task one",
             "description": "Goal: task one.", "depends_on": [], "blocks": [], "parent": "E1",
             "status": "approved", "intent": "build", "technical_constraints": ["stack: backend"]},
            {"local_id": "T2", "project_key": "SCRUM", "issue_type": "task", "summary": "Task two",
             "description": "Goal: task two, depends on task one.", "depends_on": ["T1"], "blocks": [],
             "parent": "E1", "status": "approved", "intent": "build", "technical_constraints": ["stack: backend"]},
            {"local_id": "S1", "project_key": "SCRUM", "issue_type": "subtask", "summary": "Subtask under T1",
             "description": "Goal: subtask.", "depends_on": [], "blocks": [], "parent": "T1",
             "status": "approved", "intent": "build", "technical_constraints": ["stack: backend"]},
            {"local_id": "D1", "project_key": "SCRUM", "issue_type": "task", "summary": "Not yet approved",
             "description": "Goal: not ready.", "depends_on": [], "blocks": [], "parent": None,
             "status": "draft", "intent": "build", "technical_constraints": []},
        ]
        backlog_path.write_text(json.dumps(backlog, indent=2), encoding="utf-8")

        server = _FakeJiraServer("SCRUM")
        env = {"JIRA_EMAIL": "x@example.com", "JIRA_API_TOKEN": "tok", "JIRA_HOST": "example.atlassian.net"}
        with mock.patch.dict("os.environ", env), mock.patch("urllib.request.urlopen", side_effect=server.handle):
            key_map = jira_sync.push_backlog_to_jira(backlog_path)

        ok = (
            set(key_map) == {"E1", "T1", "T2", "S1"}  # D1 (still draft) never pushed
            and "parent" not in server.issues[key_map["E1"]]
            and server.issues[key_map["T1"]]["parent"]["key"] == key_map["E1"]
            and server.issues[key_map["S1"]]["parent"]["key"] == key_map["T1"]
            and (key_map["T1"], key_map["T2"]) in server.links  # T1 blocks T2 (T2 depends_on T1)
        )
        print(f"key_map: {key_map}")
        print(f"links: {server.links}")
        print("PASS" if ok else "FAIL: parenting or dependency link wired incorrectly")
        if not ok:
            return False

        on_disk = json.loads(backlog_path.read_text(encoding="utf-8"))
        persisted_ok = all(
            item.get("jira_key") == key_map[item["local_id"]]
            for item in on_disk if item["local_id"] in key_map
        )
        print(f"jira_key persisted back to {backlog_path.name}: {persisted_ok}")
        if not persisted_ok:
            print("FAIL")
            return False

        calls_before = len(server.calls)
        with mock.patch.dict("os.environ", env), mock.patch("urllib.request.urlopen", side_effect=server.handle):
            key_map_2 = jira_sync.push_backlog_to_jira(backlog_path)
        idempotent_ok = key_map_2 == key_map and len(server.calls) == calls_before
        print(f"idempotent re-run made {len(server.calls) - calls_before} new Jira calls (expected 0): "
              f"{'PASS' if idempotent_ok else 'FAIL'}")
        return idempotent_ok
    finally:
        shutil.rmtree(root, ignore_errors=True)


def check_retry_escalated_ticket() -> bool:
    print("\n=== check 7: task_loop.retry_escalated_ticket ===")
    root = Path(tempfile.mkdtemp(prefix="loop-eng-retry-escalated-"))
    try:
        ticket = schemas.BacklogItem(
            local_id="SCRUM-77", project_key="SCRUM", issue_type="task",
            summary="Flaky ticket", description="Goal: flaky.",
            status="approved", intent="build", loop_status="escalated",
            technical_constraints=["stack: backend"],
        )

        with mock.patch.object(checkpoint, "read_checkpoint", return_value=None), \
             mock.patch.object(jira_sync, "fetch_ticket") as mock_fetch:
            outcome_missing = task_loop.retry_escalated_ticket("SCRUM-77", root)
        no_checkpoint_ok = outcome_missing == "not_started" and not mock_fetch.called
        print(f"no checkpoint -> {outcome_missing!r}, fetch_ticket called: {mock_fetch.called}: "
              f"{'PASS' if no_checkpoint_ok else 'FAIL'}")
        if not no_checkpoint_ok:
            return False

        fake_checkpoint = {
            "stalled_at_stage": "review", "last_feedback": "[review] fix the docstring",
            "attempts": {}, "history": [],
        }

        with mock.patch.object(checkpoint, "read_checkpoint", return_value=fake_checkpoint), \
             mock.patch.object(jira_sync, "fetch_ticket", return_value=ticket), \
             mock.patch.object(task_loop, "run_one_ticket", return_value=("done", {})) as mock_run, \
             mock.patch.object(jira_sync, "mark_ticket_done") as mock_done, \
             mock.patch.object(jira_sync, "unmark_ticket_escalated") as mock_unmark, \
             mock.patch.object(checkpoint, "clear_checkpoint") as mock_clear:
            outcome_ok = task_loop.retry_escalated_ticket("SCRUM-77", root)

        kwargs = mock_run.call_args.kwargs
        success_ok = (
            outcome_ok == "done"
            and kwargs.get("max_attempts") == 1
            and kwargs.get("initial_feedback") == "[review] fix the docstring"
            and kwargs.get("resume") is True
            and mock_done.called and mock_unmark.called and mock_clear.called
        )
        print(f"success: max_attempts=1, diagnosis seeded, resume=True, Jira+checkpoint synced: "
              f"{'PASS' if success_ok else 'FAIL'}")
        if not success_ok:
            return False

        with mock.patch.object(checkpoint, "read_checkpoint", return_value=fake_checkpoint), \
             mock.patch.object(jira_sync, "fetch_ticket", return_value=ticket), \
             mock.patch.object(task_loop, "run_one_ticket", return_value=("escalated", {})), \
             mock.patch.object(jira_sync, "mark_ticket_done") as mock_done_2, \
             mock.patch.object(jira_sync, "unmark_ticket_escalated") as mock_unmark_2, \
             mock.patch.object(checkpoint, "clear_checkpoint") as mock_clear_2:
            outcome_fail = task_loop.retry_escalated_ticket("SCRUM-77", root)

        failure_ok = (
            outcome_fail == "escalated"
            and not mock_done_2.called and not mock_unmark_2.called and not mock_clear_2.called
        )
        print(f"failure: Jira/checkpoint left untouched: {'PASS' if failure_ok else 'FAIL'}")
        return failure_ok
    finally:
        shutil.rmtree(root, ignore_errors=True)


def main() -> int:
    results = {
        "_to_backlog_item mapping": check_to_backlog_item(),
        "pm_agent ordering": check_pm_agent_ordering(),
        "task_router stack selection": check_task_router_stack_selection(),
        "jira_sync actionable error": check_jira_actionable_error(),
        "run_backlog happy path": check_run_backlog_happy_path(),
        "run_backlog Jira-fetch failure": check_run_backlog_jira_fetch_failure(),
        "push_backlog_to_jira": check_push_backlog_to_jira(),
        "retry_escalated_ticket": check_retry_escalated_ticket(),
    }
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    ok = True
    for name, passed in results.items():
        print(f"  {'PASS' if passed else 'FAIL'}: {name}")
        ok = ok and passed
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
