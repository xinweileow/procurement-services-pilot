# Etiqa Procurement System

A new procurement (Source-to-Pay) system that integrates with, but does not modify, the
existing Etiqa Finance Portal. See `docs/kb/business_kb.md`, `docs/kb/technical_kb.md` and
`docs/kb/ui_ux.md` for the requirements this is built from, and `.loop-eng/` for the
engineering-pipeline conventions (ticket scope, developer/reviewer skills) this repo follows.

## Backend — `src/Procurement.Api`

ASP.NET Core (.NET 10) Web API, attribute-routed Controllers, FluentValidation, RFC 7807
`ProblemDetails` for every non-2xx response, EF Core (InMemory provider for now — no real
database has been provisioned yet).

```bash
dotnet build
dotnet test
dotnet run --project src/Procurement.Api   # serves GET /api/v1/health
```

## Frontend — `frontend/`

React 18+ (Vite) + TypeScript, function components + hooks only. `src/components/`
(presentational), `src/features/<name>/` (feature-scoped logic + components), `src/api/`
(typed client — `apiRequest` parses the backend's `ProblemDetails` error shape), `src/hooks/`,
`src/types/`.

```bash
cd frontend
npm install
npm run dev     # local dev server
npm run test    # Vitest + React Testing Library
npm run build   # production build
```

## Known gaps (flagged, not silently worked around)

- `.loop-eng/pipeline/test_agent.py` and `guardrails.py` (and therefore
  `.loop-eng/hooks/pre-push`) are hardcoded to a Python/pytest target repo (`pytest tests/`,
  `ruff`, `.venv`, `requirements.txt`). This repo's confirmed stack is .NET + React, so that
  automated gate does not run against this codebase as shipped — it would fail the regression
  stage every time (no `tests/` directory, no Python to lint). Self-verification for tickets in
  this repo instead runs `dotnet build && dotnet test` and `npm run build && npm run test`
  directly. Fixing `test_agent.py`/`guardrails.py` to detect the target repo's real stack (or
  vendoring a stack-aware replacement) is still open.
