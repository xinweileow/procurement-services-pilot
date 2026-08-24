# Phase 1 proof — status

Phase 1 (plan/dev-suite-plans: "Build the homebrew coding agent, standalone,
provable on one synthetic ticket before touching Jira/backlog at all").

**Phase 2 skill authoring has since started — see "Phase 2: skill files
authored" below.** This file's Phase 1 sections are unchanged from when
Phase 1 was declared code-complete.

## What's built (`.loop-eng/pipeline/`)

All ported/authored per the plan's step-by-step instructions and Reference
implementations mapping. Every module imports cleanly and has been exercised
for real (see below) except where noted.

- `copilot_cli.py` — the coding-agent engine. Wraps `copilot -p` /
  `copilot --agent <name> -p`, encodes every confirmed Phase 0 finding
  (Windows `shutil.which` fix, real `--allow-tool` syntax, `--model auto`
  only, zero tool access for verdict skills, retry-until-valid-json).
  `CopilotUnavailableError` / `CopilotInvocationCrashed` / `CopilotJsonFormatError`
  mirror the predecessor's `anthropic.APIConnectionError`/`RateLimitError`
  split so `task_loop.py`'s defer-vs-retry-vs-crash-clean branches line up.
- `worktree.py`, `target_repo.py` — ported from `loopengineering`, generalized
  (dropped the FastAPI-specific scaffold — Phase 2 concern). New:
  `get_pr_review_state` (PR review-state polling, plan's Critical files entry).
- `guardrails.py`, `test_agent.py` — ported as-is / with defaults trimmed.
- `scope_guard.py` — rewritten for **post-hoc** enforcement (Copilot's native
  file access bypasses the predecessor's inline `propose_scope` tool-call
  interception — see the plan's Reference implementations section on this
  exact problem). Ledger concept ported, enforcement mechanism is new.
- `gate.py` — the independent post-hoc gate: guardrails → tests → scope →
  structural-reviewer, in that order (cheap deterministic checks before the
  one stage that costs a real Copilot invocation).
- `task_loop.py` — the retry loop. All 3 of Phase 1's in-scope failure kinds
  wired: code-fixable (gate rejects), crash-mid-invocation (force-clean
  worktree), human-PR-rejection (`CHANGES_REQUESTED` → retry on same
  branch). Jira/GitHub-API-failure handling is Phase 2 (no Jira wiring yet).
- `kpi_log.py`, `checkpoint.py`, `ticket.py` — supporting pieces.

## Skills authored (`.loop-eng/agent-templates/`)

- `structural-reviewer.md` — the real, for-keeps skill (Phase 1 step 3b),
  persona copied verbatim from `structural_review_agent.py`'s
  `SYSTEM_PROMPT`, translated per "How to author a skill file".
- `dev-throwaway.md` — the minimal, disposable developer skill (Phase 1 step
  3a), thrown away once Phase 2 authors the real `developer-*.md` skills.

## What's been verified for real (live Copilot)

- `dev-throwaway.md` smoke test: **PASSED**. A real `copilot -p` invocation
  ran pytest, found a seeded bug, fixed it, re-ran pytest — independently
  re-verified (own pytest run, own source read), ~100s.
- `structural-reviewer.md`'s retry-until-valid-json logic: **PASSED** as a
  deterministic unit test (monkeypatched `copilot_cli.run_copilot`).
- `structural-reviewer.md` against a real `git diff`: **first attempt
  failed** — same failure family as Phase 0 spike #4 finding #2: even with
  zero tool access, the model tried to `git add`/`commit` (correctly
  blocked) and returned an implementation status report instead of the
  required JSON. Persona strengthened to match the wording that got spike
  #4 to 8/10 (explicit "no prose/explanation/status report", "this diff has
  already been written by someone else"). **Not yet re-verified** — quota
  ran out before the retest could complete.

## The blocker

The `xinweileow` Copilot seat hit **"You have exceeded your monthly quota"**
mid-development (confirmed twice, including on a bare
`copilot -p "say hello"`). Every remaining live-invocation step is blocked
until quota resets or different credentials are supplied.

## What's been verified WITHOUT live Copilot (`dry_run_proof.py`)

Per the user's direction ("finish code now, verify later"), `task_loop.py`'s
orchestration logic has been verified as much as possible without live
Copilot: real git worktree isolation, real staging, real guardrails
(including the secrets scan catching a planted fake AWS key), real pytest
runs, the real post-hoc scope ledger, real checkpoint-file writes, real KPI
log entries, and a real `git push` to a local bare remote (never GitHub —
see `scratch_repo.py`'s docstring for why: pushing/opening PRs needs the
user's explicit go-ahead even for a throwaway repo, and there's no need to
ask when a local bare repo proves the same git plumbing). Only the two LLM
call sites (`copilot_cli.run_copilot`, `copilot_cli.run_verdict_skill`) are
replaced with scripted fakes — standing in for "some invocation happened and
produced this output", not for whether the real model would produce it.

Both scenarios **PASS**:
1. `dry_run_happy_path` — forced crash on attempt 1 (worktree force-cleaned;
   confirmed the crash-time partial edit never reached the final commit) →
   a code-fixable regression failure on attempt 2 (the fake developer
   satisfies the ticket's own test but leaves an unrelated seeded bug,
   exactly the gap the post-hoc gate's full regression suite exists to
   catch) → fixed on attempt 3, gate passes, commits, pushes, opens a
   (stub) PR → the PR stub returns `CHANGES_REQUESTED` → attempt 4 retries
   and lands → `done`, 4 developer attempts total, well inside the 5-attempt
   cap. One run exercises all three of Phase 1's in-scope failure kinds.
2. `dry_run_escalation` — a developer fake that always leaks a fake secret
   (always fails guardrails), `max_attempts=2` → exhausts, escalates,
   writes a real checkpoint file to `.loop-eng/.loop/state/`.

This found and fixed one real bug along the way: `task_loop.py` originally
called both `target_repo.checkout_task_branch` (checks the branch out in the
base repo) AND `worktree.add_worktree` (checks the same branch out again in
an isolated worktree) — git correctly refused the second checkout. Fixed by
dropping the redundant `checkout_task_branch` call; worktree mode only needs
`sync_main` before `add_worktree` branches off `DEFAULT_BRANCH` directly.

## What's still needed before Phase 1 can be called done

1. Re-run `smoke_test_skills.py`'s reviewer-against-a-real-diff check (and
   ideally `retest_reviewer.py 5`+ for a real pass rate, same discipline as
   spike #4) once quota is available, to confirm the strengthened persona
   actually fixed the observed failure mode rather than just plausibly
   addressing it.
2. Run `run_proof.py` — the real end-to-end proof against live Copilot.
   Written, not yet executed. Same narrative as `dry_run_proof.py`'s happy
   path, but every developer/reviewer response comes from the real model
   instead of a script.

## Files

```
.loop-eng/
├── pipeline/                    # the coding-agent engine (see above)
├── agent-templates/             # dev-throwaway.md, structural-reviewer.md
├── phase1-proof/
│   ├── README.md                 # this file
│   ├── scratch_repo.py           # shared scaffold (local bare remote, seeded bugs)
│   ├── pr_stub.py                # local PR/review-state stand-in (no GitHub)
│   ├── ticket_def.py             # the one hand-written synthetic ticket
│   ├── smoke_test_skills.py      # Phase 1 step 3/8: smoke-test both skills
│   ├── smoke_test_phase2_skills.py  # Phase 2 step 8, partial (see below)
│   ├── retest_reviewer.py        # N-run reliability check, reviewer only
│   ├── run_proof.py              # REAL end-to-end proof (not yet run — quota)
│   └── dry_run_proof.py          # quota-independent orchestration proof (PASSED)
├── data/loop/                    # kpi_log.jsonl, module_ownership.json (gitignored)
└── .loop/state/                  # checkpoint files (gitignored)
```

## Phase 2: skill files authored

All 7 remaining skill `.md` files are now written to `.loop-eng/agent-templates/`,
following "How to author a skill file" steps 1-7 (deterministic — no live
Copilot needed) in the plan's stated authoring order: `kb-writer.md` →
`requirement-reviewer.md` / `failure-diagnoser.md` → `spec-writer.md` →
`developer-backend.md` / `developer-dotnet.md` → `developer-frontend.md`.
Every skill's Phase 1 build order dependency (`structural-reviewer.md`
authored + smoke-tested first) already holds.

**Two real discrepancies found between the plan's Skills table and the
actual `loopengineering` source, resolved by following the real source
over the plan's description (per step 1: "copy the real SYSTEM_PROMPT...
don't paraphrase"):**
- `kb_agent.py` only has Business and Technical `SYSTEM_PROMPT`s — no
  UI/UX prompt exists anywhere in `loopengineering`, despite the plan's
  Skills table claiming "three concrete system prompts
  (Business/Technical/UI-UX)". `kb-writer.md`'s UI/UX pass (`docs/kb/ui_ux.md`)
  is therefore extrapolated from the same extract-don't-invent discipline as
  the other two passes, not ported from a real prompt — flagged here since
  it's the one pass this skill produces with no production precedent.
- `developer_agent.py`'s real `SYSTEM_PROMPT` has 7 generic rules (propose-
  scope-once, read-before-write, don't overwrite existing files, companion
  test, tool-use discipline, retry-feedback-first) — it does **not** contain
  the "HTTP status codes, structured `{"detail": ...}` errors, typed Pydantic
  validation, FK pre-validation, UTF-8 discipline" content the plan's Skills
  table attributes to it. `developer-backend.md` ports the real 7 rules
  verbatim-in-spirit, then layers the plan's explicitly-requested FastAPI
  conventions on top as additional rules — those conventions are reasonable
  and match `[REF]_etiqa_agent/dotnet-api.agent.md`'s equivalent .NET rules,
  but they are the plan's addition, not `developer_agent.py`'s.

**Other authoring notes:**
- `spec-writer.md` adds a `scope` field (file paths/globs) to each drafted
  ticket, separate from the prose "Scope:" bullets in the Description — the
  real `organise_agent.py` has no such field (its `ScopeGuard` enforcement
  was inline, keyed off a `propose_scope` tool call at *implementation*
  time, not backlog-drafting time). Needed because Phase 1's post-hoc
  `scope_guard.py` checks a ticket's declared scope against what a Copilot
  invocation actually touched, and that declaration has to come from
  *somewhere* now that there's no inline `propose_scope` tool call to
  intercept — the plan's own Reference implementations section says this
  scope declaration happens "up front, in the ticket Description or a
  first-turn scope declaration." Drafting time is the only up-front point
  left in this design, so `spec-writer.md` is where it has to live.
- `spec-writer.md` also declares each build ticket's tech stack (`stack:
  backend` / `dotnet` / `frontend`) as the first `technical_constraints`
  entry, matching the plan's "single skill per ticket" routing-scope rule —
  `task_router.py` doesn't yet read this field (that's Phase 2's routing-
  wiring step, not done here), but the convention is fixed now so routing
  has something deterministic to key off later instead of inventing a
  parsing scheme after the fact.
- `developer-dotnet.md` ports `[REF]_etiqa_agent/dotnet-api.agent.md`
  almost entirely as-is — unlike `backend-standardisation.agent.md`, it was
  already written "execute directly, no unnecessary confirmation," so there
  was no greet/wait/confirm loop to strip. Only real changes: dropped the
  VS Code `tools:`/`model:` frontmatter (per Phase 0 spike #7), dropped the
  `react-ui-agent`/`engineering-rapid-prototyper` collaboration section
  (those agents don't exist in this pipeline), and added an explicit "stop
  and escalate instead of guessing" section for the auth-middleware/PingID/
  route-renaming confirmation gates mined from `backend-standardisation.agent.md`
  (plan's Reference implementations point 3 — chat-gated confirmations
  become escalation triggers under `--no-ask-user`, since there's no chat
  turn to pause on mid-invocation).

**Smoke testing (step 8) — partial, blocked on the same quota exhaustion
as the rest of Phase 1:**
`smoke_test_phase2_skills.py` is written for the three skills cheap to
smoke-test against the existing `run_copilot`/`run_verdict_skill` pattern:
`requirement-reviewer.md` and `failure-diagnoser.md` (same zero-tool-access,
single-JSON-verdict shape as `structural-reviewer.md`, already proven in
Phase 1) and `kb-writer.md` (real source doc in, checks all three KB files
get written with required headers and the Open TBD item preserved). Not yet
run — same `xinweileow` quota block, re-confirmed at the start of this
session (`copilot -p "say hello"` still returns "You have exceeded your
monthly quota").

**Not smoke-tested, and not a trivial extension of the existing pattern:**
`spec-writer.md` (this skill is meant to run *interactively* in Copilot
Chat with a human present — its own frontmatter says so — so a scripted
smoke test would need a scripted "human" answering/deferring clarifying
questions, not just a one-shot invocation) and `developer-backend.md` /
`developer-dotnet.md` / `developer-frontend.md` (each needs a real scratch
scaffold in its actual stack — a minimal FastAPI app, a minimal ASP.NET
Core project, a minimal React app — not just a bare git repo with one
Python file, the way `dev-throwaway.md`'s smoke test gets away with). Left
as follow-up work, ideally once quota is available to iterate against a
real invocation rather than guessing at scaffold shape blind.

## Phase 2: pre-push hook + copilot.md (quota-independent, both done and verified)

Continuing the same "quota-independent work while the seat is exhausted"
thread (re-confirmed blocked again this session — fresh quota error,
different request ID). Two Phase 2 items that don't touch live Copilot at
all:

- **`.loop-eng/hooks/pre-push` + `.loop-eng/hooks/pre_push_check.py`**
  (Phase 2 step 6). The actual check logic lives in the `.py` file so it's
  independently testable; the `pre-push` file itself is the thin shell shim
  git actually looks for by name. Re-runs guardrails + the full regression
  suite, plus a secrets scan over the real **push range** (parsed from
  git's pre-push stdin contract: `<local ref> <local sha1> <remote ref>
  <remote sha1>`) rather than the staged index — nothing is staged by push
  time in the normal case, so the existing guardrails.py secrets scan
  (`git diff --cached`) would silently no-op there. Required extracting a
  new public `guardrails.scan_diff_text_for_secrets(diff_text)` so both the
  mid-loop staged-index scan and this push-range scan share one regex
  implementation instead of two.

  **Verified for real** (`test_pre_push_hook.py`): installs the actual hook
  into a scratch repo with `.loop-eng` vendored in (matching the plan's real
  "cloned/submoduled into each target repo" deployment shape — Phase 1's
  own `scratch_repo.py` deliberately doesn't do this, since Phase 1 runs the
  pipeline orchestrator-side), then does real `git push` calls against a
  real local bare remote. All 4 cases pass: a clean push is allowed, a
  push with a planted fake secret is blocked (citing the secrets scan), a
  push that breaks the regression suite is blocked (citing the regression
  suite), and — as a control, not a failure — the same leaky commit pushed
  with `--no-verify` succeeds, confirming pre-flight item 5's known gap
  (this is only the local half of the gate; no server-side/CI check exists
  yet) is real and correctly un-enforced by design, not accidentally
  bypassed some other way.

- **`.loop-eng/copilot.md`** (Phase 2 step 1's last piece — the 5 skill
  files were already done; this is the routing doc itself). Documents both
  interactive flows: Kickoff (kb-writer → spec-writer → human approval →
  jira_sync push) and Escalation (load the checkpoint → load the same
  developer skill + diagnosis history → present the plan's lettered
  decision brief verbatim → act on A/B/C/D). Flags honestly, rather than
  asserting: whether Copilot CLI actually auto-loads this file as project
  context (vs. a human needing to explicitly point Chat at it) is still
  unconfirmed — the `--no-custom-instructions` help text mentions
  `AGENTS.md and related files` but Phase 0 never spike-tested whether
  `copilot.md` under `.loop-eng/` qualifies.

## Still open (Phase 2)

- Re-verify `structural-reviewer.md`'s persona fix + run
  `smoke_test_phase2_skills.py` + run `run_proof.py` — all blocked on quota.
- Port `jira_sync.py`, `pm_agent.py`, `task_router.py`, and `schemas.py`
  (`BacklogItem`/`Backlog`) from `loopengineering` — needed to wire
  `task_loop.py` to actually poll Jira and pick tickets (Phase 2 steps 3-4).
  Not started — the biggest remaining chunk, deliberately not rushed into
  this same pass alongside the hook/copilot.md work.
- Server-side / CI counterpart to the pre-push hook (pre-flight item 5) —
  not built, not blocked on quota, just not done yet.

## Phase 2: Jira/backlog wiring (quota-independent, done and verified)

Completes what the previous entry called "the biggest remaining chunk."
Ported and wired, all in `.loop-eng/pipeline/`:

- **`schemas.py`** (`BacklogItem`/`Backlog`, pydantic) — ported from the
  predecessor, extended with two fields the predecessor never needed:
  `scope: list[str]` (spec-writer.md Rule 9 — the post-hoc scope
  declaration, since there's no inline `propose_scope` tool call anymore to
  intercept it at implementation time) and `technical_constraints: list[str]`
  (spec-writer.md Rule 7's `stack: backend`/`dotnet`/`frontend` convention).
  A computed `test_path` property always returns `None` — Jira-sourced
  tickets have no single canonical acceptance-test file the way Phase 1's
  hand-written synthetic ticket did; `gate.py` already handles that
  gracefully (falls through to "just run the full regression suite").
- **`jira_sync.py`** — ported, with the plan's called-out new capability:
  every `_request()` failure now raises `JiraAPIError` carrying a
  human-actionable `.detail` (401 → "token expired, renew at
  id.atlassian.com, update .env.local"; 403 → permissions; 429 → back off;
  5xx → transient) instead of a bare `RuntimeError`.
- **`pm_agent.py`** — ported verbatim (deterministic next-ticket ordering).
- **`task_router.py`** — tier classification ported (with `model` dropped
  entirely, since Copilot only accepts `--model auto` — spike #8), plus a
  **new** `select_developer_skill()`: parses the `stack: ...` convention
  spec-writer.md now fixes, with a loud fallback-and-warn (not a silent
  guess) when a ticket has none.
- **`gate.py`** — now also runs `requirement-reviewer.md` (between the
  scope check and `structural-reviewer.md`) — Phase 1 shipped
  structural-review-only since requirement-reviewer.md wasn't authored yet.
- **`task_loop.py`** — new `run_backlog()`: polls Jira → `pm_agent` picks
  the next ticket → `task_router` selects tier + developer skill →
  `run_one_ticket` → `jira_sync.mark_ticket_done`/`mark_ticket_escalated`.
  Every Jira call is wrapped separately in `JiraAPIError` handling — printed
  and deferred, never fed to diagnosis, never counted against the 5-attempt
  cap (Full flow's 4th failure kind, the one Phase 1 explicitly left for
  Phase 2). Also added a `--jql`/`--limit`/`--auto-approve-prs` CLI (`python
  -m pipeline.task_loop <target_dir>`). `push_and_create_pr`/
  `check_pr_merged`/`get_pr_review_state` are injectable (same pattern as
  `run_one_ticket`), so a scratch/local-remote test never needs real GitHub.
- **`checkpoint.py`** — fixed to serialize either Phase 1's dataclass
  `Ticket` or Phase 2's pydantic `BacklogItem` (`dataclasses.asdict` doesn't
  work on a pydantic model).

**Verified for real** (`.loop-eng/phase2-proof/test_jira_wiring.py`, all 6
checks pass) — network-boundary-mocked (`urllib.request.urlopen`, not
jira_sync's own functions), so `_to_backlog_item`'s ADF-flattening/label
parsing, `_request`'s HTTP-error-to-actionable-detail mapping, and
`run_backlog`'s full real orchestration all run for real:
1. A realistic Jira issue JSON maps to `BacklogItem` correctly.
2. `pm_agent.pick_next_task` correctly skips epics/parents/escalated/
   unapproved/unmet-dependency/non-build tickets in a 6-item mixed backlog.
3. `task_router.select_developer_skill` resolves all three stacks plus the
   fallback case.
4. A mocked 401 raises `JiraAPIError` with a `.detail` that actually names
   the problem and the fix, not a bare "HTTP 401".
5. `run_backlog` end-to-end (mocked Jira fetch + mocked Copilot, same
   schema-aware fake as `dry_run_proof.py`, injected local PR stub, real
   git worktree/gate/scope/commit/push against a local bare remote):
   ticket reaches `done`, `jira_sync.mark_ticket_done` is called with the
   right key.
6. `run_backlog` when the Jira fetch itself fails: returns `{}` rather than
   crashing or misattributing the failure to a ticket.

This found and fixed two more real bugs: `run_backlog` had no way to inject
a PR backend at all (would always hit the real `gh`-backed `target_repo.py`
functions, which have nothing to talk to against a local-bare-remote
scratch repo — fixed by threading `push_and_create_pr`/`check_pr_merged`/
`get_pr_review_state` through, same injection pattern `run_one_ticket`
already had); and the scope ledger
(`.loop-eng/data/loop/module_ownership.json`) is real, shared, persistent
state across every proof script ever run against this repo — a new test
reusing a file path (`app/calc.py`) that an earlier, unrelated test's
ticket had already claimed under a different `local_id` spuriously
collided. Fixed in the test via an isolated ledger path, not by changing
`scope_guard.py` itself — the collision detection is correct real behavior,
the test was just polluting shared state.

## Still open (updated)

- Re-verify `structural-reviewer.md`'s persona fix, run
  `smoke_test_phase2_skills.py`, and run `run_proof.py` — all blocked on
  quota (re-confirmed exhausted again this session).
- `spec-writer.md`, `developer-backend.md`/`developer-dotnet.md`/
  `developer-frontend.md` still have no smoke test at all (see the earlier
  "Not smoke-tested" note — needs real per-stack scratch scaffolds).
- Server-side / CI counterpart to the pre-push hook (pre-flight item 5).
- The chat-based kickoff flow itself (Phase 2 step 2) — `copilot.md`
  documents it, but nothing drives it end-to-end; that's inherently a live
  Copilot Chat session, not something scriptable the way the rest of this
  has been.
- `main.py` (chat-triggered kickoff entry point, per the plan's folder
  structure) doesn't exist yet — `task_loop.py` now has a CLI, but the
  kickoff-side entry point (upload docs → kb-writer → spec-writer → Jira
  push) is still just `copilot.md`'s prose instructions, not code.

## Phase 2: `main.py` kickoff entry point (quota-independent, done and verified)

Closes the previous entry's last gap. `kb-writer`/`spec-writer` (Kickoff Flow
steps 1-3) run as personas *inside* Copilot Chat — nothing to launch there.
Step 4 (push the human-approved backlog to Jira) was the one step that
actually needed code and had none: `jira_sync.py` could fetch tickets and
mark them done/escalated, but had no way to *create* issues in Jira at all.

- **`jira_sync.py`** — new `create_issue()`, `link_depends_on()`, and
  `push_backlog_to_jira()`. Reads spec-writer.md's approved local backlog
  (`.loop-eng/data/backlog/<project_key>-backlog.json`, the JSON array Rule
  11 writes), creates epics → stories/tasks → subtasks via a parent-
  resolution worklist (not a hardcoded 3-level order, so it degrades
  gracefully if a ticket's parent is missing or not yet approved), then
  links every `depends_on` via Jira's "Blocks" link type. `parent_key` is
  used uniformly for both epic-linking and subtask-parenting — same
  team-managed-Jira assumption `_to_backlog_item` already makes reading
  `fields.parent` back. Both issue creation and link creation write their
  result (`jira_key` / `_linked_depends_on`) back to the file on disk
  immediately, so a `JiraAPIError` partway through (expired token, rate
  limit) leaves a resumable file — re-running skips everything already done
  instead of duplicating issues or links. Only `status == "approved"`
  tickets are ever pushed.
- **`.loop-eng/main.py`** (new file) — the actual chat-triggered entry
  point: `python .loop-eng/main.py <backlog.json>`. Deliberately thin — argv
  parsing plus a call to `push_backlog_to_jira`, surfacing
  `JiraAPIError.detail` on failure instead of a raw traceback. This is now
  the concrete command `copilot.md`'s Kickoff Flow step 4 tells Copilot Chat
  to run via its shell tool.
- **`copilot.md`** — step 4 updated from vague "run the CLI" prose to the
  literal command, plus a line on relaying `JiraAPIError.detail` to the human
  verbatim and that re-running after a fix is safe (no duplicates).

**Verified for real** (`test_jira_wiring.py`, extended to 7 checks — check 6
is new): a mocked Jira issue-create/link server (`urllib.request.urlopen`
mocked at the network boundary, `create_issue`/`link_depends_on`/`_request`
all exercised for real) proves, against a 5-item backlog (epic → 2 tasks, one
depending on the other → a subtask → a still-draft ticket):
1. Epic/task/subtask parenting round-trips correctly (`fields.parent` set to
   the right key at every level, absent on the epic).
2. The `depends_on` link is created in the correct outward/inward direction.
3. The draft ticket is never pushed.
4. `jira_key` is persisted back to the backlog file on disk.
5. A second run against the same file makes **zero** new Jira calls —
   confirmed idempotent, including link creation (which needed its own
   `_linked_depends_on` tracking field — without it, re-running would have
   silently created duplicate Jira links every time, since only issue
   creation had a natural idempotency signal (`jira_key`) and link creation
   didn't; caught and fixed before writing the test, not after a failure).

## Still open (updated again)

- Re-verify `structural-reviewer.md`'s persona fix, run
  `smoke_test_phase2_skills.py`, and run `run_proof.py` — all blocked on
  quota (re-confirmed exhausted again this session).
- `spec-writer.md`, `developer-backend.md`/`developer-dotnet.md`/
  `developer-frontend.md` still have no smoke test at all (needs real
  per-stack scratch scaffolds).
- Server-side / CI counterpart to the pre-push hook (pre-flight item 5).
- The chat-based kickoff flow's steps 1-3 (kb-writer → spec-writer →
  human approval) and Phase 0 spike #6 (does Copilot Chat render the
  Escalation Flow's decision brief as clickable buttons) still need a live
  interactive Copilot Chat session — inherently not scriptable.
- Escalation Flow's `--retry-escalated <ticket-id>` path (`copilot.md`
  Flow 2, step 4B) is referenced in prose and in `pm_agent.py`'s own skip
  message, but no such flag exists on `task_loop.py`'s CLI yet, and
  `pick_next_task` unconditionally skips every escalated ticket regardless.
  Not built this session — out of scope for the Kickoff-flow work above,
  flagged as the next real gap in Flow 2.

## Phase 2: Escalation Flow's retry-escalated path (quota-independent, done and verified)

Closes the gap the previous entry flagged.

- **`jira_sync.py`** — new `fetch_ticket(issue_key)`: single-issue
  counterpart to `fetch_tickets_from_jira`, needed because a retried ticket
  isn't guaranteed to still match whatever JQL originally surfaced it.
- **`task_loop.py`** — `run_one_ticket` gained two new params:
  `initial_feedback` (seeds attempt 1's prompt with a diagnosis instead of
  starting blank) and `resume` (threaded into `worktree.add_worktree`,
  which already had a `resume` parameter built for exactly this — reuses
  whatever worktree the failed run left in place instead of force-recreating
  it from `DEFAULT_BRANCH`). New `retry_escalated_ticket(ticket_id,
  base_repo_dir, ...)`: reads the ticket's checkpoint, fetches its current
  Jira state, resolves its developer skill, then calls `run_one_ticket` with
  `max_attempts=1` (this is "one more retry" per copilot.md's option B, not
  a fresh 5-attempt cycle — that cap already ran out once), seeded with the
  checkpoint's `last_feedback` and `resume=True`. On `"done"`: marks Jira
  Done, clears the `loop-escalated` label, clears the checkpoint. Any other
  outcome: Jira and the checkpoint are left exactly as `run_one_ticket`
  itself left them (it already rewrites the checkpoint with a fresh
  diagnosis if this attempt also exhausts) — copilot.md's rule that a
  checkpoint only ever clears once a ticket reaches `done`.
- **`.loop-eng/main.py`** — restructured into subcommands (`push-backlog`,
  now joined by `retry-escalated <target_dir> <ticket-id>`), since this is
  now the second concrete action copilot.md tells Copilot Chat to run
  directly via its shell tool. `run_backlog` (the unattended background
  loop) deliberately still isn't exposed here — copilot.md's own framing
  note says that's infrastructure, not something chat drives.
- **`copilot.md`** — Escalation Flow option B now gives the literal command
  instead of just naming the `--retry-escalated <ticket-id>` path in prose.

**Verified for real** (`test_jira_wiring.py`, now 8 checks — check 7 is
new, mocking `checkpoint`/`jira_sync`/`run_one_ticket` directly rather than
re-proving `run_one_ticket`'s own internals, which check 5a already covers
for real): no checkpoint -> defers without ever calling `fetch_ticket`;
success -> `run_one_ticket` is called with exactly the resume contract this
path promises (`max_attempts=1`, the checkpoint's diagnosis seeded in,
`resume=True`) and Jira + the checkpoint are synced; failure -> Jira and the
checkpoint are left untouched.

## Phase 2: server-side CI counterpart to the pre-push hook (quota-independent, done and verified)

Closes pre-flight item 5's other half: "Pre-push hook is bypassable
(`--no-verify`) — needs a server-side second line of defense (CI re-check /
branch protection), not just the local hook."

- **`.loop-eng/hooks/ci_check.py`** (new) — imports and calls
  `pre_push_check.check_push()` directly rather than reimplementing it, so
  there's one implementation of "guardrails + secrets scan over the push
  range + regression suite" with two trigger points. The only new logic is
  `_event_shas()`: resolves the `(local_sha, remote_sha)` pair
  `check_push()` expects from a GitHub Actions event's environment
  variables instead of git's pre-push stdin contract (CI has no real
  `git push` invocation to read stdin from) — `pull_request` events use the
  PR's base/head SHAs (exactly the diff under review), `push` events use
  before/after (falling back to the empty-tree SHA for a new branch's first
  push, matching `pre_push_check.py`'s own new-ref handling).
- **`.loop-eng/hooks/loop-eng-ci-check.yml`** (new) — a GitHub Actions
  workflow **template**, not installed anywhere live. It's meant to be
  copied to `<target-repo>/.github/workflows/` when vendoring `.loop-eng`
  into a real target repo, the same way `pipeline/`+`hooks/` themselves get
  vendored (GitHub only discovers workflow files at a repo's own top-level
  `.github/workflows/`, not inside a vendored subdirectory — so this can't
  usefully live anywhere else and self-activate). Deliberately **not**
  written to this repo's own `.github/workflows/` or wired into any live
  GitHub branch-protection setting — doing that for real means picking an
  actual target repo and touching its live CI/repo settings, which needs
  the human's explicit go-ahead on that specific repo, not something to do
  unilaterally while proving the pipeline code itself works.
- **What's still a manual step, on purpose:** this workflow only makes a
  bad push fail visibly in CI. Turning that into an actually-enforced gate
  (closing the `--no-verify` bypass) requires a repo admin to also add a
  branch-protection rule on the real target repo requiring the `loop-eng
  gate` job to pass before merge — a GitHub repo-settings change, not
  something a workflow YAML file can turn on by itself. The workflow's own
  header comment says this explicitly, so it isn't silently assumed done
  once the file exists.

**Verified for real** (`.loop-eng/phase1-proof/test_ci_check.py`, 3/3
cases pass — `check_push()`'s own checks are already proven via a real
`git push` in `test_pre_push_hook.py`, so this test's job is proving
`ci_check.py`'s new SHA-resolution logic specifically): a `push` event with
a clean commit is allowed; a `push` event with a planted secret is blocked,
citing the secrets scan; a `pull_request` event (base/head SHAs, with the
push-event env vars deliberately left unset so a wrong fallback would
`KeyError` instead of silently passing) catches the same secret via the
other branch of `_event_shas()`.

## Still open (updated again)

- Re-verify `structural-reviewer.md`'s persona fix, run
  `smoke_test_phase2_skills.py`, and run `run_proof.py` — all blocked on
  quota (re-confirmed exhausted again this session).
- `spec-writer.md`, `developer-backend.md`/`developer-dotnet.md`/
  `developer-frontend.md` still have no smoke test at all (needs real
  per-stack scratch scaffolds).
- The chat-based kickoff flow's steps 1-3 (kb-writer → spec-writer →
  human approval) and Phase 0 spike #6 (does Copilot Chat render the
  Escalation Flow's decision brief as clickable buttons) still need a live
  interactive Copilot Chat session — inherently not scriptable.
- Actually installing `loop-eng-ci-check.yml` on a real target repo, plus
  the branch-protection rule that makes it an enforced gate rather than a
  visible-but-ignorable CI failure — needs a specific target repo and the
  human's go-ahead on touching its live settings.
- `.loop-eng/SETUP.md` (see below) covers vendoring/credentials/hook/CI
  install as a documented procedure, but has only been reviewed, not
  exercised against a real fresh target repo end-to-end — that would need
  an actual new repo to bootstrap into.

## `main.py`'s location — deviation resolved, moved to match the plan

The previous entry flagged `main.py` living at `.loop-eng/main.py` instead
of the plan's documented `.loop-eng/pipeline/main.py`, justified at the time
as avoiding `-m pipeline.main`/cwd ceremony. That justification didn't
actually hold up: `task_loop.py` (already inside `pipeline/`) uses relative
imports (`from . import checkpoint, ...`) and has *never* been runnable as
a bare script (`python .loop-eng/pipeline/task_loop.py` fails with
"attempted relative import with no known parent package") — it has always
required `python -m pipeline.task_loop`. Keeping `main.py` outside the
package avoided nothing real; it just made `main.py` the one file in the
whole pipeline with a different import convention from its siblings, to
save a `cd .loop-eng &&` prefix in two documented commands.

Moved to `.loop-eng/pipeline/main.py`, using the same relative imports as
every other pipeline module. Invocation is now `python -m pipeline.main
push-backlog data/backlog/<project_key>-backlog.json` /
`python -m pipeline.main retry-escalated <target_dir> <ticket-id>`, run
with `.loop-eng/` as the working directory — `copilot.md`'s two command
blocks updated to match (`cd .loop-eng && python -m pipeline.main ...`).
Verified: `python -m pipeline.main --help` and both subcommands' `--help`
resolve correctly from `.loop-eng/` as cwd; `pipeline.main` imports cleanly
as a package member.

## `.loop-eng/SETUP.md` — bootstrap doc for a fresh target repo (new, not yet exercised for real)

Answers the question this session raised: if loop-eng starts on a different
device or a different (fresh) target repo, should there be a doc Copilot
follows to set itself up before the Kickoff/Escalation flows apply? Yes —
this closes pre-flight items 2 ("New target repo bootstrapping... isn't
specified") and 3 ("Credential provisioning for a fresh environment...
needs an explicit setup step") as a documented procedure, the same style as
`copilot.md` itself (imperative, addressed to Copilot, "ask the human"
called out explicitly wherever a decision is genuinely open or the action
is risky).

Six steps: (1) vendor `.loop-eng` in — submodule vs. plain copy is asked of
the human, not silently picked, since the plan's own folder-structure
comment ("cloned/submoduled") never decided between them; (2) confirm the
target repo's own `.gitignore` actually excludes `.env`/`.env.local`/
`.loop-eng/.loop/`/`.loop-eng/data/` before anything sensitive gets written,
since a fresh repo has none of that yet; (3) credentials + a
`copilot -p "say hello"` sanity check before proceeding further; (4) install
the local pre-push hook; (5) install `loop-eng-ci-check.yml`, then
explicitly **stop and ask the human** before touching branch-protection
settings — the same "don't unilaterally touch live CI/repo settings" line
this session already drew, now written into the procedure itself rather
than only enforced by whoever's driving in the moment; (6) confirm the
target repo's own `.github/agents/*.md` skill files are present.

`copilot.md` now points here explicitly: before either flow, check whether
`.loop-eng/pipeline/` exists yet — if not, follow `SETUP.md` first.

**Not yet done:** running this procedure for real against an actual fresh
target repo (this session's loopyengineering repo already has `.loop-eng`
vendored at its own root, so it isn't itself a "fresh target repo" test
case) — that's the next real verification opportunity once a concrete
target repo is named.

## The three previously-undecided pre-flight items — all now decided by the human

Pre-flight items 6 and 14 were flagged as genuinely open, not something to
resolve by guessing. All three now have an explicit answer:

1. **`input_guard.py`'s prompt-injection tripwire — carry forward. Done.**
   Ported near-verbatim to `.loop-eng/pipeline/input_guard.py` (pure regex,
   zero deps, unchanged from the predecessor). The only new piece is the
   entry point: the predecessor called this inline from Python
   (`doc_upload.save_business_doc`); this restructure has no Python-mediated
   upload step, so it's now a CLI (`python -m pipeline.main sanitize-doc
   <path> --in-place`) that `copilot.md`'s Kickoff Flow runs as its new
   step 1, before `kb-writer.md` ever sees a raw upload. Non-blocking, same
   as the predecessor: flagged lines are redacted in place, not rejected —
   Copilot relays what was flagged to the human rather than silently
   continuing.
2. **`mcp_requirement_agent.py` + `mcp_setup_agent.py` — carry forward as a
   new skill. Done.** The plan's own framing ("carry this forward as a
   seventh Copilot-invoked skill... or treat it as out of scope") already
   anticipated this couldn't port as a plain Python function — the
   predecessor's detection step calls `anthropic.Anthropic()` with a forced
   `tool_choice`, and this org has no Anthropic API access at all
   (pre-flight item 15). New skill file
   `.loop-eng/agent-templates/mcp-requirement-detector.md`, authored via
   the same "How to author a skill file" procedure as every other skill
   (real persona copied over, forced-tool-call schema translated to a
   closing fenced-JSON instruction, zero tool access, "the KBs given to you
   are complete and authoritative" per Rule 2). The deterministic
   surrounding logic (known-services directory, report/credentials YAML
   shape, server-file scaffolding, catalog updates, env-var saving) ported
   near-verbatim into two new modules, `pipeline/mcp_requirement.py` and
   `pipeline/mcp_setup.py`, with paths generalized from the predecessor's
   single global `REPO_ROOT` to explicit `target_dir` — same convention
   every other pipeline module already follows. Wired into `copilot.md` as
   a new, deliberately non-blocking Kickoff step 3 (after KBs are built,
   before spec-writer runs) — detecting integrations and offering to
   scaffold them never gates ticket drafting.

   **One thing NOT resolved, flagged rather than guessed at:** the
   predecessor's `_register_mcp_server` shells out to `claude mcp add`
   unconditionally. No Phase 0 spike has confirmed whether or how Copilot
   CLI registers local MCP servers, so `mcp_setup.py` still only tries
   `claude` (kept in case a dev machine has Claude Code installed
   alongside Copilot) and otherwise prints the manual registration path,
   rather than inventing a `copilot mcp add` syntax that might not exist.
3. **`--allow-tool write --allow-tool shell` scope — leave it broad.
   Decided, no change made.** The human's call: Copilot keeps unrestricted
   write/shell access in an unaudited target repo, same as it already had.
   No code change follows from this — recorded here so this pre-flight item
   reads as "decided: no" rather than "still open" in any future pass over
   this checklist.

**Verified for real** (new `.loop-eng/phase2-proof/test_new_capabilities.py`,
6/6 checks pass, `copilot_cli.run_verdict_skill` mocked at the same boundary
`test_jira_wiring.py` already established): `input_guard` catches known
injection shapes and leaves normal lines alone, and its CLI sanitizes in
place with the right exit code; `detect_mcp_requirements` builds the
expected message (KBs + connected-servers catalog) and parses a mocked
verdict; `write_report` correctly sorts a detected integration into
`already_connected` (catalog match), a known service's `needs_setup` entry
(real `env_var`/`where_to_get_key`), or an unknown service's generic entry
(derived `env_var`) — and `load_credentials` round-trips a partially-filled
copy correctly; `run_mcp_detection` end-to-end reads real KB files and
writes the report to the right path; `mcp_setup.setup_from_credentials`
creates the server directory, saves the env var, and updates the catalog,
with npm/claude mocked absent so the test stays fast and network-free (same
graceful-degradation path `guardrails.py`'s ruff-optional handling already
proves). `main.py` extended with three more subcommands (`sanitize-doc`,
`detect-mcp-requirements`, `setup-mcp`) alongside the existing two —
verified `python -m pipeline.main --help` lists and resolves all five.
