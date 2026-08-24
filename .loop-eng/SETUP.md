# SETUP.md — bootstrapping loop-eng into a fresh target repo

You are Copilot. This doc is for the ONE-TIME setup that has to happen
before `.loop-eng/copilot.md`'s Kickoff/Escalation flows apply: getting
`.loop-eng` actually vendored, credentialed, and gated in a target repo that
doesn't have it yet — a new device, a new project, or picking up an
existing project that was never wired up. If a human explicitly points you
at this file (e.g. "set up loop-eng in this repo"), or you find yourself
about to run `copilot.md`'s Kickoff Flow against a repo where
`.loop-eng/pipeline/` doesn't exist yet, follow this first, in order. Once
step 5 is done, `copilot.md` applies normally and this file isn't needed
again for this repo.

This closes two gaps the plan's own pre-flight checklist flagged as
unresolved: "New target repo bootstrapping... isn't specified" and
"Credential provisioning for a fresh environment... needs an explicit setup
step." Where the plan left something genuinely undecided, this doc says so
and asks the human — it doesn't silently pick for them.

---

## Step 1 — Vendor `.loop-eng` into this repo

The plan's own folder-structure comment says `.loop-eng` is "cloned/
submoduled into each target repo" without picking one. Ask the human which
they want before doing either:

- **Git submodule** (recommended default if they have no preference):
  `.loop-eng` stays version-tracked against its source repo;
  `git submodule update --remote` pulls updates later. Slightly more git
  ceremony (submodule init/update on every fresh clone).
  ```
  git submodule add <loop-eng-source-repo-url> .loop-eng
  ```
- **Plain vendored copy**: simpler, no submodule ceremony, but updates mean
  manually re-copying `pipeline/`, `agent-templates/`, `copilot.md`,
  `hooks/`, `SETUP.md` from the source and re-committing — nothing tracks
  drift from the source automatically.

Either way, don't guess — this is the kind of infrastructure choice the
human should make once, explicitly, not have decided for them silently.

## Step 2 — Make sure this repo's `.gitignore` actually excludes generated state

A fresh target repo has none of this yet. Confirm the repo's root
`.gitignore` (create it if missing) includes:

```
.env
.env.local
.loop-eng/.loop/
.loop-eng/data/
```

Without this, the next step's credentials file — or a later run's live
checkpoint/KPI state — could get committed by accident. Check before
writing anything sensitive, don't assume it's already covered.

## Step 3 — Credentials

Create `.loop-eng/.env.local` (now gitignored per step 2) with:

```
JIRA_EMAIL=...
JIRA_API_TOKEN=...
JIRA_HOST=...
```

Also confirm Copilot CLI's own GitHub auth is present in the *environment*
(not a file) — `COPILOT_GITHUB_TOKEN` → `GH_TOKEN` → `GITHUB_TOKEN`, in that
precedence (Phase 0 spike #2, confirmed). Then sanity-check Copilot CLI
itself is authenticated and not quota-exhausted before doing anything else:

```
copilot -p "say hello"
```

If that fails, stop here and relay the actual error to the human — every
later step depends on this working.

## Step 4 — Install the local pre-push hook

```
cp .loop-eng/hooks/pre-push .git/hooks/pre-push
chmod +x .git/hooks/pre-push
```

(On Windows, Git Bash respects the executable bit the same way; a plain
`cp` from Git Bash is enough — no separate chmod needed on NTFS, but running
it anyway is harmless.)

## Step 5 — Install the CI counterpart, then STOP before enabling it as a gate

```
mkdir -p .github/workflows
cp .loop-eng/hooks/loop-eng-ci-check.yml .github/workflows/loop-eng-ci-check.yml
git add .github/workflows/loop-eng-ci-check.yml
git commit -m "loop-eng: install CI counterpart to the pre-push hook"
git push
```

Committing and pushing this file only makes a bad push fail *visibly* in
CI — it does not block a merge by itself. Turning it into an actually
enforced gate needs a branch-protection rule (repo Settings → Branches →
require the "loop-eng gate" status check before merging) — a live setting
that affects every contributor on this repo, not something to flip on
unilaterally. **Ask the human explicitly before touching branch-protection
settings**, and only on the specific repo they confirm. If they're not
ready to decide that yet, leave it as a visible-but-unenforced CI check and
say so plainly, rather than silently enabling or silently skipping it.

## Step 6 — Confirm the target-repo skill files exist

`.github/agents/` (not `.loop-eng/agent-templates/` — Phase 0 spike #7
confirmed Copilot only discovers custom personas relative to its own cwd
in the target repo checkout) needs:

```
kb-writer.md, spec-writer.md, requirement-reviewer.md,
structural-reviewer.md, failure-diagnoser.md,
developer-backend.md, developer-dotnet.md, developer-frontend.md
```

If any are missing, copy them from `.loop-eng/agent-templates/` into
`.github/agents/` and commit. Don't edit their content here — this step is
about presence, not authoring; skill content changes belong in the source
`.loop-eng/agent-templates/`, not a per-target-repo fork.

## Done

Steps 1-6 complete → `.loop-eng/copilot.md`'s Kickoff Flow (upload docs,
draft a backlog, push to Jira once approved) is ready to use normally in
this repo. This file doesn't need to be revisited unless bootstrapping a
*different* target repo.
