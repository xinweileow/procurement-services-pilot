# Phase 0 spike findings

Real results from running the spike scripts in this directory against Copilot CLI 1.0.80, authenticated as `xinweileow` (the same account referenced in `loopengineering/src/pipeline/config.py`'s `TARGET_REPO_URL`). Each spike script independently re-verifies its own claim rather than trusting Copilot's self-report — same discipline the plan requires from the real pipeline.

## Spike #1 — does `copilot -p` loop internally? **CONFIRMED: YES**

`spike_01_loop_internally.py` — a single `copilot -p` invocation, given a deliberately buggy `add()` function and a failing test, ran pytest, read the source, found and fixed the bug, and re-ran pytest to confirm — all inside one invocation, no external orchestration between steps. Independently re-verified afterward (own `pytest` run, `git diff` on the actual file) rather than trusting Copilot's own "PASS" report.

**Validates the plan's core architecture** (`copilot_cli.py` brackets one invocation per ticket) — the stage-by-stage fallback documented in the plan's Phase 0 spike #1 entry stays as insurance but isn't needed.

## Spike #2 — headless/unattended auth. **CONFIRMED: YES**

`spike_02_headless_auth.py` — ran `copilot -p` in a child process environment stripped down to just `PATH` + `GH_TOKEN` (no `COPILOT_*` credential-store state passed through), sourcing the token from `gh auth token`. Authenticated and responded correctly with no interactive login. Matches `copilot help environment`'s documented precedence: `COPILOT_GITHUB_TOKEN` → `GH_TOKEN` → `GITHUB_TOKEN`.

**Unblocks Phase 1/2 automation** — `pipeline/` can run fully unattended with just a token in the environment.

## Spike #3 — concurrency. **CONFIRMED: no collision**

`spike_03_concurrency.py` — 3 concurrent `copilot -p` invocations, each in its own scratch repo, each asked to write a file containing its own unique marker. All 3 wrote the correct marker, no cross-contamination in output or file content.

## Spike #4 — JSON-extraction reliability. **Real, load-bearing methodology finding — see below**

This spike went through two revisions because the first version's result was actively misleading.

**Attempt 1 — bare `copilot -p "<instruction>"`, no `--agent`:** 0/5 runs produced a parseable JSON block. The model treated the instruction as an open-ended code-review request and gave free-form commentary (suggestions, type hints, docstrings) regardless of the "end with a fenced json block" instruction.

**Attempt 2 — real `--agent` persona (`.github/agents/requirement-reviewer.md`), `--allow-tool write` granted:** 1/10. Worse than expected, and the failure mode was different and more revealing: most runs didn't ignore the format request — they didn't believe the diff/ticket text in the prompt was real. Runs searched the filesystem for a `.patch`/`.diff` file, ran `git status`/`git log` looking for the "actual" diff, asked the user to paste a diff, or in two cases actually **attempted to implement the ticket** (created `addition.py` + tests) instead of reviewing it. **Root cause: Copilot's underlying model has strong tool-use/investigate instincts that override a text-only review task, especially when it has write/shell access and an empty directory to explore.**

**Attempt 3 — persona explicitly says the input is complete/authoritative, no tool access granted at all:** added "The ticket text and diff given to you in the user message are COMPLETE and AUTHORITATIVE... do not search the filesystem, do not run git/shell commands... do not attempt to implement" to the persona, and stopped granting `--allow-tool write` (a reviewer never needs it — removing the tool removes the temptation). **Result: 8/10 (80%) produced parseable, schema-matching JSON.**

**But the content of those 8 successes exposes a second, deeper problem:** 7 of the 8 were `{"clear_met": false, "gaps": ["No diff provided..."]}`  — the model followed the *format* instruction correctly but still didn't recognize the diff text embedded in the prompt as a real diff. Only 1/10 (run 9) both formatted correctly AND produced the semantically correct verdict (`clear_met: true`). The other 2/10 broke format entirely, back to asking for more information in prose.

**Working theory, not yet confirmed:** the test prompt's "Diff:" section was two bare `+`-prefixed lines, not a canonically-shaped unified diff (`diff --git a/... b/...`, `index ...`, `+++`/`---` headers, `@@` hunk markers) — the shape `git diff --cached` actually produces, which is what the real `requirement_review_agent.py` always feeds it. The model may be refusing to treat non-diff-shaped text as "the diff" no matter how forcefully the persona insists it's authoritative. **This needs one more retest with a real, canonically-formatted diff (from an actual `git diff`) before drawing a final conclusion** — not done in this session; flagged as the next spike-4 iteration.

**Load-bearing takeaways regardless of the final number:**
1. A structured-output skill's persona must explicitly forbid tool use and explicitly assert the given input is complete, or the model's "let me go investigate" instinct corrupts the review before formatting even becomes a problem (0/5 → 1/10 → 8/10 across the three attempts, same underlying fix each time).
2. **Format compliance and semantic correctness are two separate failure modes, and this data shows the second is real** — exactly the "valid JSON but semantically wrong" risk the plan's own JSON-reliability fallback section already warned it can't catch (that's what Phase 3's human-advisory period exists for). This isn't hypothetical anymore; it's observed. `requirement-reviewer`/`structural-reviewer`/`failure-diagnoser` all need real-diff-shaped test cases in their smoke tests, not toy snippets, or the smoke test will pass while the real skill silently produces wrong verdicts on real tickets.
3. **"How to author a skill file" needs a new explicit step** for the three verdict-shaped skills: grant no write/shell tools, state plainly that the message content is the complete and only input, and smoke-test with a real `git diff` output, not a hand-written snippet.

## Spike #5 — native edit fidelity / path-scoped writes. **CONFIRMED, corrected pattern**

An early ad hoc test of `--allow-tool 'write(allowed_dir)'` blocked writes to *both* the allowed and a forbidden path — looked like a CLI bug. `spike_05_edit_scope.py` isolated the cause: **bare `write(<dir>)` (no glob) does not scope correctly; `write(<dir>/*)` does.**

| Pattern | Allowed write succeeded | Forbidden write blocked |
|---|---|---|
| `write(allowed)` | ❌ No | ✅ Yes (but so was the allowed one) |
| `write(allowed/*)` | ✅ Yes | ✅ Yes |
| `write(<absolute-path>)` | ❌ No | ✅ Yes |

**Use `write(<dir>/*)` everywhere a scoped-write pattern is needed** — bare directory names and absolute paths without a glob suffix both fail closed (over-block) rather than fail open, which is at least the safe failure direction, but not the intended one.

## Spike #7 — custom agent file format and location. **CONFIRMED**

`spike_07_agent_file_format.py` — wrote a minimal agent to `.github/agents/spike-test-agent.md` with `name:`/`description:` YAML frontmatter, invoked via `copilot --agent spike-test-agent -p "..."`, confirmed the persona body was actually followed.

**Confirmed format:**
- **Location:** `.github/agents/<agent-name>.md` — in the **target repo**, not `.loop-eng/skills/` (discovery is relative to Copilot's cwd, same reasoning the plan already applied to `docs/kb/*.md`)
- **Frontmatter:** YAML, at minimum `name:` + `description:`
- **Invocation:** `copilot --agent <agent-name> -p "..."`
- **Body:** everything after the frontmatter is the persona, followed exactly

Matches exactly what `[REF]_etiqa_agent/dotnet-api.agent.md` itself pointed at ("this project's rules live in `.github/agents/`") — that hint was correct.

**Bonus finding, not previously in the plan:** Copilot CLI *also* has a completely separate `skill` mechanism (`copilot skill list`/`add`/`remove`) — `SKILL.md` files auto-discovered from `.github/skills/`, `.agents/skills/`, `.claude/skills/` (project) or `~/.copilot/skills/`, `~/.agents/skills/` (personal), always-loaded and available rather than explicitly selected per-invocation. Don't confuse this with `--agent` — the plan needs deterministic per-ticket selection via `task_router.py`, which only `--agent` gives.

## Spike #8 — model pinning + which models are available. **PARTIAL — real risk to the multi-model resilience plan**

`spike_08_model_pinning.py` — `copilot help config` documents a real catalog of ~26 settable model names across Claude/GPT/Gemini/Grok/Kimi families. Tested `--model` against a sample plus `auto`.

**Result: every explicit model name was rejected** ("Model \"X\" from --model flag is not available.") — **only `--model auto` works** under this account. Confirmed via `--output-format json` in manual testing that `auto` really does route different requests to different real models (`gpt-5-mini`, `claude-haiku-4.5` both observed) — but that's Copilot's own router deciding, not something `copilot_cli.py` can pin per-skill.

**Real problem for the cross-model-tension resolution** (running `structural-reviewer.md` on a deliberately different model than `developer-*.md` to avoid correlated failure) — as tested, that mitigation is not achievable.

**Before treating this as final**, two things this spike can't answer from outside:
1. Is `xinweileow` using the org's real enterprise Copilot seat, or a more limited individual/trial seat with different entitlements?
2. Does model selection route through a different mechanism for enterprise orgs (admin-configured allowlist, different flag, web-console setting)?

**If this holds for the real org seat:** the plan's Reference implementations "meta-lesson" bullet needs revisiting — the correlated-failure risk stands unmitigated by explicit model diversity; auto-routing's natural variance is the only (weaker, non-deterministic) fallback.

## Not yet run

- **Spike #6 (Copilot Chat decision-UI element)** — not testable via the CLI at all; requires manually driving Copilot Chat in VS Code to see whether a lettered decision brief renders as clickable buttons or plain "reply with a letter" text.

## Confirmed corrections to the plan, independent of pass/fail status

- `--allow-tool edit,shell` (assumed syntax) → real syntax is `--allow-tool write --allow-tool shell` (or `--allow-tool 'shell(cmd:*)'` to scope to specific commands). "edit" is not a real tool name.
- Windows: `subprocess.run(["copilot", ...])` fails with `FileNotFoundError` — npm installs it as `copilot.CMD`, and Windows `CreateProcess` doesn't resolve PATHEXT shims from a bare name the way `cmd.exe` does. Resolve via `shutil.which("copilot")` first.
- Windows: redirected stdout defaults to cp1252 — Copilot's own output routinely contains Unicode (box-drawing characters, checkmarks, em dashes) that cp1252 can't encode, crashing any script that prints it without `sys.stdout.reconfigure(encoding="utf-8")`. Same fix `task_loop.py` already documents for the real pipeline.
