"""Jira sync — the task loop's ticket source + completion sync. Fetches
implementation-ready tickets directly from Jira and marks issues Done on
merge, cascading to auto-close a parent once every sibling under it is Done
too. Uses direct REST calls via urllib (no extra dependency), same pattern
as the predecessor's jira_sync.py/jira_push.py.

Ported from loopengineering/src/pipeline/jira_sync.py (plan's Reference
implementations: "mostly port/adapt, but jira_sync.py needs one genuinely
new capability the predecessor doesn't have: actionable-error surfacing when
a Jira/GitHub API call fails, instead of the predecessor's silent defer").

That capability is JiraAPIError: every _request() failure raises it instead
of a bare RuntimeError, carrying a human-actionable `.detail` (what failed,
how to fix it) alongside the normal exception message. Per the plan's Full
flow, this failure kind is handled differently from every other stage
failure by whatever's driving the loop (task_loop.py's run_backlog, not
run_one_ticket itself — Jira calls happen in the outer per-ticket loop, not
mid-invocation): never fed to a diagnosis skill (a code fix cannot resolve
an expired token), and never counted against MAX_LOOP_RETRY_ATTEMPTS.
"""
from __future__ import annotations

import base64
import json
import os
import urllib.error
import urllib.request
from pathlib import Path

from .config import JIRA_DONE_TRANSITION_ID
from .schemas import Backlog, BacklogItem

DEFAULT_JQL = "project = SCRUM AND parent is not EMPTY AND statusCategory != Done ORDER BY created ASC"

_ISSUE_TYPE_MAP = {"subtask": "task", "story": "story", "epic": "epic", "task": "task", "bug": "task"}


class JiraAPIError(RuntimeError):
    """A Jira REST call failed. `.detail` is a human-actionable message —
    what failed and how to fix it — meant to be surfaced directly to the
    human (via Copilot Chat if a session is open, or the next place they
    look), not just logged. Never code-fixable: callers must defer, not
    diagnose-and-retry, and must not count this against the retry cap."""

    def __init__(self, message: str, detail: str):
        super().__init__(message)
        self.detail = detail


def _missing_env_error(name: str) -> JiraAPIError:
    return JiraAPIError(
        f"Jira credential {name} is not set",
        detail=(
            f"Jira API call failed — the environment variable {name} is not set. "
            f"Set it in .loop-eng/.env.local (gitignored, for local credential "
            f"overrides) and re-run. Ticket left not_started; retried automatically "
            f"once this is fixed."
        ),
    )


def _auth_header() -> str:
    email = os.environ.get("JIRA_EMAIL")
    token = os.environ.get("JIRA_API_TOKEN")
    if not email:
        raise _missing_env_error("JIRA_EMAIL")
    if not token:
        raise _missing_env_error("JIRA_API_TOKEN")
    creds = base64.b64encode(f"{email}:{token}".encode()).decode()
    return f"Basic {creds}"


def _host() -> str:
    raw = os.environ.get("JIRA_HOST", "").strip()
    if not raw:
        raise _missing_env_error("JIRA_HOST")
    return raw.replace("https://", "").replace("http://", "").rstrip("/")


def _actionable_detail(method: str, path: str, code: int, body: str) -> str:
    if code == 401:
        return (
            f"Jira API returned 401 (Unauthorized) on {method} {path} — the "
            f"JIRA_API_TOKEN in .loop-eng/.env.local has likely expired. Renew it "
            f"at https://id.atlassian.com/manage-profile/security/api-tokens, "
            f"update .env.local, then re-run."
        )
    if code == 403:
        return (
            f"Jira API returned 403 (Forbidden) on {method} {path} — the "
            f"authenticated account (JIRA_EMAIL) likely lacks permission for this "
            f"project/issue. Check project permissions in Jira, then re-run."
        )
    if code == 429:
        return f"Jira API rate-limited (429) on {method} {path} — back off and re-run shortly."
    if code >= 500:
        return f"Jira returned a server error ({code}) on {method} {path} — likely transient, re-run shortly."
    return f"Jira {method} {path} failed: HTTP {code}: {body[:500]}"


def _request(method: str, path: str, body: dict | None = None) -> dict | None:
    url = f"https://{_host()}{path}"
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(
        url, data=data,
        headers={"Authorization": _auth_header(), "Content-Type": "application/json"},
        method=method,
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            raw = resp.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        response_body = e.read().decode(errors="replace")
        raise JiraAPIError(
            f"Jira {method} {path} failed: HTTP {e.code}",
            detail=_actionable_detail(method, path, e.code, response_body),
        ) from e
    except urllib.error.URLError as e:
        raise JiraAPIError(
            f"Jira {method} {path} failed: network error: {e.reason}",
            detail=(
                f"Could not reach Jira ({_host()}) for {method} {path}: {e.reason}. "
                f"Check network connectivity and JIRA_HOST, then re-run."
            ),
        ) from e


def _adf_to_text(description: dict | None) -> str:
    """Jira Cloud REST v3 returns descriptions in Atlassian Document Format,
    not plain text — flatten paragraph text nodes back to a plain string."""
    if not description or not description.get("content"):
        return ""
    text_parts = []
    for block in description["content"]:
        for node in block.get("content", []):
            if "text" in node:
                text_parts.append(node["text"])
    return "".join(text_parts)


def _intent_from_labels(labels: list[str]) -> str:
    for label in labels:
        if label in ("intent-build", "intent-decision"):
            return label.removeprefix("intent-")
    return "unknown"


def _to_backlog_item(issue: dict) -> BacklogItem:
    fields = issue["fields"]
    issue_type_name = fields["issuetype"]["name"].lower()
    labels = fields.get("labels", [])
    parent_key = fields.get("parent", {}).get("key") if fields.get("parent") else None
    status_obj = fields.get("status", {})
    status_name = status_obj.get("name", "").lower()
    status_cat = status_obj.get("statusCategory", {}).get("key", "").lower()
    is_done = (status_name == "done" or status_cat == "done")
    return BacklogItem(
        local_id=issue["key"],
        project_key=issue["key"].split("-")[0],
        issue_type=_ISSUE_TYPE_MAP.get(issue_type_name, "task"),
        summary=fields["summary"],
        description=_adf_to_text(fields.get("description")),
        depends_on=[],
        blocks=[],
        parent=parent_key,
        status="done" if is_done else "approved",
        intent=_intent_from_labels(labels),
        loop_status="escalated" if "loop-escalated" in labels else "not_started",
    )


def fetch_tickets_from_jira(jql: str = DEFAULT_JQL) -> Backlog:
    result = _request(
        "POST", "/rest/api/3/search/jql",
        {
            "jql": jql, "maxResults": 100,
            "fields": ["summary", "description", "issuetype", "parent", "status", "labels"],
        },
    )
    items = [_to_backlog_item(issue) for issue in result.get("issues", [])]
    return Backlog(items=items)


def fetch_ticket(issue_key: str) -> BacklogItem:
    """Single-issue counterpart to fetch_tickets_from_jira -- the Escalation
    Flow's `--retry-escalated <ticket-id>` path (task_loop.retry_escalated_ticket)
    needs one ticket's current Jira state, not a JQL-scoped backlog page that
    might not even include it."""
    result = _request(
        "GET",
        f"/rest/api/3/issue/{issue_key}?fields=summary,description,issuetype,parent,status,labels",
    )
    return _to_backlog_item(result)


def _get_transitions(issue_key: str) -> list[dict]:
    result = _request("GET", f"/rest/api/3/issue/{issue_key}/transitions")
    return result.get("transitions", [])


def _transition_to_done(issue_key: str) -> None:
    transitions = _get_transitions(issue_key)
    done_transition = next(
        (
            t for t in transitions
            if t.get("name", "").lower() == "done"
            or t.get("to", {}).get("statusCategory", {}).get("key") == "done"
            or str(t.get("id")) == str(JIRA_DONE_TRANSITION_ID)
        ),
        None,
    )
    if not done_transition:
        return
    _request("POST", f"/rest/api/3/issue/{issue_key}/transitions", {"transition": {"id": done_transition["id"]}})


def _update_labels(issue_key: str, add: str | None = None, remove: str | None = None) -> None:
    ops: list[dict] = []
    if add:
        ops.append({"add": add})
    if remove:
        ops.append({"remove": remove})
    if not ops:
        return
    _request("PUT", f"/rest/api/3/issue/{issue_key}", {"update": {"labels": ops}})


def mark_ticket_escalated(issue_key: str) -> None:
    _update_labels(issue_key, add="loop-escalated")


def unmark_ticket_escalated(issue_key: str) -> None:
    _update_labels(issue_key, remove="loop-escalated")


def _get_parent_key(issue_key: str) -> str | None:
    result = _request("GET", f"/rest/api/3/issue/{issue_key}?fields=parent")
    parent = result["fields"].get("parent")
    return parent["key"] if parent else None


def _children_all_done(parent_key: str) -> bool:
    result = _request(
        "POST", "/rest/api/3/search/jql",
        {"jql": f"parent = {parent_key}", "maxResults": 100, "fields": ["status"]},
    )
    issues = result.get("issues", [])
    if not issues:
        return False
    return all(i["fields"]["status"]["statusCategory"]["key"] == "done" for i in issues)


def mark_ticket_done(issue_key: str) -> None:
    _transition_to_done(issue_key)
    parent_key = _get_parent_key(issue_key)
    if parent_key and _children_all_done(parent_key):
        _transition_to_done(parent_key)


_ISSUE_TYPE_NAMES = {"epic": "Epic", "story": "Story", "task": "Task", "subtask": "Subtask"}


def _text_to_adf(text: str) -> dict:
    """Inverse of _adf_to_text: wrap plain text as a minimal ADF doc (one
    paragraph per non-empty line) so created issues round-trip through the
    same document shape fetch_tickets_from_jira reads back."""
    lines = [line for line in text.split("\n") if line.strip()] or [""]
    return {
        "type": "doc", "version": 1,
        "content": [{"type": "paragraph", "content": [{"type": "text", "text": line}]} for line in lines],
    }


def create_issue(project_key: str, issue_type: str, summary: str, description: str, parent_key: str | None = None) -> str:
    """Creates one Jira issue, returns its key. `parent_key` is used for both
    a subtask's parent story/task AND a story/task's parent epic — mirrors
    how `_to_backlog_item` above reads `fields.parent` generically at every
    hierarchy level, which only works on a team-managed ("next-gen") Jira
    project. A company-managed project would need the epic link via a
    separate custom field instead; not handled here."""
    fields: dict = {
        "project": {"key": project_key},
        "issuetype": {"name": _ISSUE_TYPE_NAMES.get(issue_type, "Task")},
        "summary": summary,
        "description": _text_to_adf(description),
    }
    if parent_key:
        fields["parent"] = {"key": parent_key}
    result = _request("POST", "/rest/api/3/issue", {"fields": fields})
    return result["key"]


def link_depends_on(depends_on_key: str, item_key: str) -> None:
    """`depends_on_key` blocks `item_key` (item_key depends on / is blocked
    by depends_on_key) — the standard Jira "Blocks" link type read in the
    outward/inward direction that reproduces a `depends_on` relationship."""
    _request("POST", "/rest/api/3/issueLink", {
        "type": {"name": "Blocks"},
        "outwardIssue": {"key": depends_on_key},
        "inwardIssue": {"key": item_key},
    })


def push_backlog_to_jira(backlog_path: Path) -> dict[str, str]:
    """Kickoff Flow step 4 (copilot.md): push spec-writer's approved local
    backlog (`.loop-eng/data/backlog/<project_key>-backlog.json`, the JSON
    array format spec-writer.md Rule 11 writes) to Jira as real issues, then
    links every declared `depends_on`.

    Only items with `status == "approved"` are pushed — draft/flagged/
    needs_followup tickets are never created in Jira. `blocks` is not
    synced separately: it's the same relationship as `depends_on` from the
    other side, and creating a link from `depends_on` alone is sufficient.

    Idempotent and resumable: each item's `jira_key` is written back to the
    file on disk immediately after that issue is created, before moving to
    the next one, and each successful link is recorded the same way in
    `_linked_depends_on`. If a JiraAPIError interrupts the run partway
    through (expired token, rate limit, etc.), the issues/links already made
    are recorded — re-running skips them instead of creating duplicates, the
    same "ticket left as-is, human re-runs once fixed" contract as
    run_backlog's Jira-call handling. This also makes cross-run resume work
    correctly when a dependency is approved and pushed only in a later run
    than its dependent.

    Creation order follows the parent chain (epics, then their stories/
    tasks, then subtasks) via a worklist rather than assuming a fixed
    3-level order, so it degrades gracefully if a ticket's parent lives
    outside this backlog file or hasn't been approved yet — such tickets are
    left un-pushed and reported, not silently dropped or errored on.
    """
    raw: list[dict] = json.loads(backlog_path.read_text(encoding="utf-8"))
    key_map: dict[str, str] = {item["local_id"]: item["jira_key"] for item in raw if item.get("jira_key")}

    def _save() -> None:
        backlog_path.write_text(json.dumps(raw, indent=2), encoding="utf-8")

    pending = {item["local_id"]: item for item in raw if item.get("status") == "approved" and not item.get("jira_key")}
    while pending:
        progressed = False
        for local_id, item in list(pending.items()):
            parent_local = item.get("parent")
            if parent_local and parent_local not in key_map:
                continue  # this item's parent hasn't been pushed yet -- retry on a later pass
            parent_key = key_map.get(parent_local) if parent_local else None
            jira_key = create_issue(
                item["project_key"], item["issue_type"], item["summary"], item["description"],
                parent_key=parent_key,
            )
            item["jira_key"] = jira_key
            key_map[local_id] = jira_key
            _save()
            print(f"[{local_id}] created {jira_key}")
            del pending[local_id]
            progressed = True
        if not progressed:
            print(
                f"push_backlog_to_jira: stuck -- parent not resolvable (missing from this "
                f"backlog, or not yet approved) for: {', '.join(pending)}"
            )
            break

    for item in raw:
        if item.get("status") != "approved":
            continue
        item_key = key_map.get(item["local_id"])
        if not item_key:
            continue
        linked = set(item.get("_linked_depends_on", []))
        for dep_local in item.get("depends_on", []):
            if dep_local in linked:
                continue  # already linked on a prior run -- don't create a duplicate Jira link
            dep_key = key_map.get(dep_local)
            if not dep_key:
                continue  # dependency not pushed yet -- link it once a future run creates it
            link_depends_on(dep_key, item_key)
            linked.add(dep_local)
            item["_linked_depends_on"] = sorted(linked)
            _save()

    return key_map
