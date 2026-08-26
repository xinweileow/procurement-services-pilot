# UI/UX Knowledge Base — Etiqa Procurement System

Sources: the "Etiqa Smart Procurement Intake & Governance Prototype" HTML (a working interactive prototype — literal markup, IDs and inline JS state/logic), six saved pages of the existing Etiqa Finance Portal (Workspace: Dashboard, Requisitions, Approval Inbox; Finance: Finance System home, Budget, Requisition Finalisation) used as the current-state UI/technical reference, and — as of 2026-08-25 — three full literal-markup captures of the live production Finance System (ITRMS) checked into `docs/reference/*.html` (Dashboard, Budget, Requisition Finalisation), which superseded the rendered-snapshot-only versions of those same three screens. The S2P Flow document is prose/tabular process requirements and states no UI/UX detail of its own beyond process ownership — it is not a source for this file.

## Design System

**Authoritative reference: [`design-system.md`](./design-system.md).** It documents, at the literal Tailwind-class/hex-value level, the exact color tokens, layout shell (dark sidebar + topbar), and component patterns (metric cards, panels, tabs, tables, badges, pagination, filters, charts) read directly from the three saved production pages in `docs/reference/`. Procurement is a module of that same Finance System (ITRMS) and must be re-skinned to match it, not styled independently — treat any conflict between that doc and the summary bullets below (or the placeholder prototype tokens further down) in `design-system.md`'s favor.

**From the Finance Portal (existing, current-state reference):**
- Persistent top navigation bar with app switcher: Workspace, Finance, Administration (Admin Console, Settings, Audit Log), plus a user identity chip (employee ID + role, e.g. "00161173 System Admin") and Sign Out.
- Left/primary navigation per app area: Workspace exposes Dashboard, Requisitions, Approval Inbox; Finance exposes Finance System home (dashboard), Budget, Requisition Finalisation, Payment Vouchers, Vendors, Fin Ops Config, Reports, System Settings.
- Metric tiles (label + large value) used for budget health (Allocated, Available, Reserved, Spent, per CAPEX/OPEX) and status counters.
- Data tables with column headers, a text search box, status/category filter dropdowns, a page-size selector (20/50/100) and "Showing X to Y of Z entries" pagination footer, with Previous/Next pagination controls (`aria-label="Pagination"`, `"Previous page"`, `"Next page"`).
- Action buttons per table row (e.g. "View", "View Details", "Approve", "Release", "Finalise").
- Monetary values formatted as `MYR`/`RM` with thousands separators and, on the dashboard, abbreviated to `k`/`M` (e.g. "MYR 20.79M").

**From the eProcurement Prototype (target intake experience — literal markup available for mapping; palette below is superseded by `design-system.md` where they conflict, but component names like `.stepper`/`.evidence-box`/`.callout` are still the only source for those wizard components):**
- CSS custom properties define the palette: `--bg:#f4f7fb`, `--panel:#fff`, `--text:#152033`, `--muted:#667085`, `--border:#d8deea`, `--accent:#1769e0` / `--accent-dark:#0f4fa8` / `--accent-soft:#eaf2ff`, `--success:#137a4b`/`--success-soft:#eaf8f1`, `--warn:#9a5b00`/`--warn-soft:#fff5df`, `--danger:#b42318`/`--danger-soft:#fff0ee`, `--shadow:0 12px 30px rgba(21,32,51,.08)`, `--radius:16px`.
- Reusable components: `.metric` tiles, `.panel`/`.main-panel`/`.side-panel` two-column layout, `.stepper` step nav with numbered circles and active/complete states, `.form-grid` two-column form layout, `.choice`/`.choice-grid` radio/checkbox cards, `.callout` (default/warn/danger/success variants) for policy/status messaging, `.badge` (default/success/warn/danger variants) for status labels, `.evidence-box`/`.evidence-zone` drag-and-drop file upload with a `.file-list` of attached files, `.table-wrap`/`table` for tabular data, `.progress-block`/`.bar` for progress meters, `.info-button`/`.tooltip` hover/click-to-reveal explanatory tooltip, `.timeline`/`.status-card` for lifecycle/tracking stages.
- Responsive breakpoints collapse two-column grids to one column at 960px and 620px.

---

## Module: M1 — Demand Intake & Business Case

### Screens / Flows
- **Login / Role Selector** (prototype) — a role dropdown (`IT Business Requestor`, `Non-IT Business Requestor`, `Procurement Administrator`) that changes the rest of the UI's steps, menus, tabs and category options; an "Allowed access" chip list shows the capabilities granted to the selected role.
- **Catalogue Search** (prototype, requestor roles, step 1 of the role-based flow) — a search box for the intended item/service; two outcomes: "Simulate catalogue item found" (shows a sample catalogue results table and tells the user to use the catalogue purchase flow instead) or "Item not available - proceed as Non-Catalogue" (proceeds to the intake landing page).
- **Request Type / Request Details landing page** (prototype, combined step) — engagement pathway choice (Sourcing with Contract / Sourcing Only / Contract Only / Other Query, each with an info-tooltip explanation), procurement nature (New Procurement / Renewal / Variation Order / Contract Extension), and, when nature implies an existing contract, a retrieved "Existing contract retrieved" summary card plus (for Contract Extension) an extension-eligibility checklist and status callout; below that, the business-case fields (title, justification, category/department/country/entity, delivery date, criticality) and evidence upload zones (business case, scope, other supporting docs).
- **Budget & Route** (prototype step) — currency, estimated contract value, funding type, budget reference, budget approval status, RFI-only checkbox, budget-approval evidence upload, and a live "Automatic procurement route" callout (route category, applicable threshold, minimum quotations, tender/GSP-reroute flags, indicative approval level, RFQ/RFP release-gate status).
- **Supplier** (prototype step) — supplier status choice (Existing registered supplier / New-non-registered supplier / Supplier not known), conditional supplier-name/registration fields, an existing-supplier status panel (registration/3PC/ESG/Associated-Person/TPRM badges) or a new-supplier duplicate-check panel and exception-evidence upload.
- **Workspace Dashboard** (Finance Portal, existing) — "Welcome back" header, four quick-action tiles (New Requisition, My Requisitions, Approvals Inbox, Budget Console), a Budget Health metric block, a Spend Trend chart, a "Pending My Approval" table preview and a Finance Console requisition-finalisation preview.
- **Workspace — Requisitions** (Finance Portal, existing) — a "Headcount Hiring Request" list screen with a "New Requisition" action, Status and Category filters, and a requisitions table.

### Components & Interactions
- Stepper navigation (`#stepper`) lets the user jump between completed/active steps; "Previous"/"Continue" buttons advance the wizard, disabled/relabelled at the first ("Previous" disabled) and last ("Submit request") step.
- Category cards (`.category-cards`) restricted per role via `allowedCategories()`; selecting a role re-normalises the currently selected category if it is not in the new role's allowed list.
- Evidence upload zones support drag-and-drop and browse-to-upload, show a running attached-file list with remove buttons, enforce a 20 MB per-file limit client-side, and show a Required/Optional/attached-count badge per document type.
- Inline validation: amount field shows an error state (red text) for empty, non-numeric, non-positive or more-than-2-decimal-place values; the intake wizard blocks "Continue" and shows the first validation error inline (`#saveMessage`) rather than navigating.
- Info-tooltip pattern: a small circular "i" button toggles a dark tooltip panel positioned below-right of the control; dismissible via outside click or Escape.
- Duplicate-request detection surfaces a related-requests table (reference, department, supplier/category, value, submitted-within-window) only when a duplicate/anti-splitting condition is triggered.

### Source Element Mapping
| Prototype element | Component name | Props / State | Triggered action |
|---|---|---|---|
| `<select id="loginRoleSelect">` | `RoleSelector` | `value: "IT Business Requestor"\|"Non-IT Business Requestor"\|"Procurement Administrator"` | resets `step` to 0, re-renders allowed categories/steps/chips |
| `<div id="roleAccessChips">` | `RoleAccessChips` | `chips: string[]` from `roleAccessConfig[role].chips` | display-only, badge list |
| `<nav id="stepper">` | `WizardStepper` | `steps: [label, description][]`, `activeIndex`, `completeUpTo` | clicking a step sets `state.step` and re-renders |
| `<button id="saveDraftButton">` | `SaveDraftButton` | none | shows "Draft saved in this browser session." message |
| `<button id="previousButton">` / `<button id="nextButton">` | `WizardNavButtons` | `disabled` on first step; label switches to "Continue"/"Submit request" on last step | step navigation / final submit with validation |
| `input[name="pathway"]` radio group | `EngagementPathwayChoice` | `value`, tooltip text from `pathwayDefinitions` | sets `state.pathway`, re-renders |
| `<select id="natureInput">` | `ProcurementNatureSelect` | options: New Procurement, Renewal, Variation Order, Contract Extension | toggles the "Existing contract retrieved" panel and previous-ID field |
| `<input id="previousIdInput">` | `PreviousContractIdInput` | required when nature is Renewal/Variation Order/Contract Extension | text input |
| existing-contract summary block (`.renewal-box`) | `ExistingContractSummary` | supplier, original value, start/expiry date, performance rating, previous approver, cumulative tenure, expiry alert | read-only display; drives `renewalDecisionInput` |
| `#extensionClauseInput` / `#ratesChangedInput` / `#termsChangedInput` checkboxes | `ExtensionEligibilityChecklist` | booleans feeding `extensionStatus()` | recomputes "Extension eligible" / "Fresh approval / tender required" callout |
| `<input id="titleInput">`, `<textarea id="descriptionInput">`, `<select id="categoryInput">`, `<input id="departmentInput">`, `<select id="procurementCountryInput">`, `<select id="procurementEntityInput">` / `<input id="procurementEntityInput">`, `<input id="deliveryDateInput" type="date">`, `<select id="criticalityInput">` | `RequestDetailsForm` fields | see business_kb M1 checklist for exact option lists | update `state.*`, some re-render (category/country/entity) |
| `data-upload-key="businessCase"` / `"scope"` / `"otherRequest"` evidence zones | `EvidenceUpload` | `title, help, required, accept, files[]` per `documentDefinitions()` | drag/drop or browse adds files to `state.uploads[key]`; badges update |
| `<input id="budgetInput" type="number">` | `EstimatedValueInput` | validated by `amountValidationMessage()` | recomputes route/threshold/quote-count/approval-level callouts |
| `<input id="rfiOnlyInput" type="checkbox">` | `RfiOnlyToggle` | boolean | relaxes the budget-approval gate for RFQ/RFP issuance |
| `input[name="supplierMode"]` radio group | `SupplierModeChoice` | Existing registered supplier / New-non-registered supplier / Supplier not known | toggles existing-supplier status panel vs. new-supplier duplicate panel |
| catalogue search input + `#simulateCatalogueFoundButton` / `#markNonCatalogueButton` | `CatalogueSearchPanel` | `catalogueQuery`, `catalogueStatus: "not_searched"\|"found"\|"not_found"` | sets `isNonCatalogue` and advances/blocks the wizard |
| Workspace Dashboard "New Requisition" / "My Requisitions" / "Approvals Inbox" / "Budget Console" tiles | `QuickActionTile` | title + subtitle text | navigates to the corresponding screen (Finance Portal, existing) |

### UX Behavior Checklist
- [ ] The wizard must not allow "Continue" past a step with unresolved validation errors; the error message must display inline at the bottom of the step, not as a blocking dialog.
- [ ] Selecting a login role must immediately re-filter the visible category options and the step list/labels shown to the user, and must reset progress to the first step.
- [ ] The catalogue-search step must block the requestor from reaching the non-catalogue intake form unless they have either found a catalogue match (redirected message) or explicitly marked the item "not available."
- [ ] Evidence upload zones must show drag-over visual feedback and reject/ warn on files over the 20 MB limit without silently dropping them.
- [ ] The Contract Extension checklist must recompute "Extension eligible" vs. "Fresh approval / tender required" live as any of the three checkboxes or the cumulative-tenure value changes.
- [ ] The final submit action must be blocked until all four compliance declarations are checked and no blocking issue remains, surfacing the specific unmet condition to the user.

### Open Questions
- The prototype's role-based intake flow (Catalogue Search → Request Landing → Budget & Route → Supplier) coexists in the same file with an earlier, non-role-based 10-step flow (Request type → Request details → Budget & route → Supplier → Governance → Due diligence → People & duties → Documents → Review & declare → Workflow tracking); the file's own final script section states the role-based version is an "overlay" that "preserves original UI/UX components." Whether the production build should implement the full 10-step flow for Procurement Administrator processing (which the role-based overlay compresses into Governance/Due Diligence/People & Duties/Documents/Work) or the compressed overlay only is not stated.
- Whether "My Requisitions" (Finance Portal Dashboard tile) and "Requisitions" (Finance Portal Workspace nav item, showing "Headcount Hiring Request") are the same screen or two distinct requisition-type views is not stated by the source.

---

## Module: M2 — Budgeting & Budget Control

### Screens / Flows
- **Finance — Budget** (Finance Portal, existing) — a Budget Console with sub-tabs (Overview, Plan & Setup, Spend Papers, Activity, FX); the Overview tab shows Allocated/Reserved/Actual-Spend/Available Budget metric tiles with percentage-of-allocated context, a note explaining the utilisation-percentage formula, a Fiscal Year / Month / Cost Type filter row, a "Yearly Expenses Breakdown (Monthly View)" chart, and a Cost Centre Breakdown table (Cost Centre, Capex Balance (MYR), Opex Balance (MYR), Utilisation (%)).

### Components & Interactions
- Filter row with three dropdowns (Fiscal Year: FY2024–FY2028/All; Month: Jan–Dec/All; Cost Type: CAPEX/OPEX/Total) that re-filter the chart and table below.
- Metric tiles pair a headline amount with a percentage-of-allocated subtext (e.g. "0.33% of allocated").

### Source Element Mapping
- The Budget screen is captured only as a rendered Finance Portal snapshot (no literal prototype markup with IDs was provided for this screen), so no `Prototype element → Component` table is produced for it — see the Finance Portal design-system entry above for the shared table/filter/metric-tile pattern this screen reuses.

### UX Behavior Checklist
- [ ] Changing any of the Fiscal Year / Month / Cost Type filters must update both the chart and the Cost Centre Breakdown table shown below it.
- [ ] The utilisation-percentage figure must always be computed as (Reserved + Actual Spend) ÷ Allocated Budget × 100, per the on-screen note.

### Open Questions
- The Budget screen's "Plan & Setup," "Spend Papers," "Activity" and "FX" tabs are named in the navigation but their contents were not captured in the reference screenshots; their expected fields/behaviour are not stated.

---

## Module: M3 — Approval & Governance Gate

### Screens / Flows
- **Governance** (prototype, Procurement Administrator step) — "A. Requestor declarations" panel (outsourcing / single-source / emergency checkboxes, each with conditional follow-up fields and evidence upload) with an explanatory sub-line clarifying the requestor declares facts while the system/Procurement determine the resulting route; an anti-splitting related-requests table appears when triggered.
- **People & Duties** (prototype step) — business-user summary block plus editable role-assignment fields (technical/business contact, procurement lead, technical evaluator, commercial evaluator, approver) with a segregation-of-duties conflict/clear callout and the Conflict of Interest Declaration evidence upload.
- **Workspace — Approval Inbox** (Finance Portal, existing) — "Pending My Approval" summary card plus an "All Approval Requests" table with Status and Request Type filters, a search box, and a data table (Request ID, Request Type, Submitter, Amount, Cost Centre, Submitted On, Status, Action) with View Details/Approve actions.

### Components & Interactions
- Checkbox-triggered conditional sections (outsourcing/single-source/emergency) that reveal additional required fields and an evidence-upload zone only when checked.
- Segregation-of-duties callout recomputes live as any of the five role-holder name fields change, comparing them case-insensitively for exact-name collisions.
- Approval Inbox status filter options: All, Draft, Pending Approval, Approved, Rejected, Cancelled, Executed, Returned for Rework; request-type filter includes at least "Hiring Request" (from the sample data).

### Source Element Mapping
| Prototype element | Component name | Props / State | Triggered action |
|---|---|---|---|
| `#outsourcingInput` / `#singleSourceInput` / `#emergencyInput` checkboxes | `RequestorDeclarationChoice` | boolean each | reveals conditional evidence/reason fields; re-renders routing panel |
| `#singleSourceCategoryInput` / `#singleSourceReasonInput` | `SingleSourceJustification` | category enum, free text | required before proceeding when single-source is checked |
| `#emergencyReasonInput` / `#disruptionImpactInput` | `EmergencyJustification` | free text | required before proceeding when emergency is checked |
| `#antiSplitJustificationInput` | `AntiSplitJustification` | free text | required when an anti-split alert is present |
| `#technicalContactInput`, `#procurementUserInput`, `#technicalEvaluatorInput`, `#commercialEvaluatorInput`, `#approverInput` | `PeopleAndDutiesForm` | free-text name per role | recomputes `sodConflict()` |
| conflict callout (`.callout` in `renderPeople`) | `SoDConflictBanner` | `conflict: boolean` | display-only, blocks submission until resolved |

### UX Behavior Checklist
- [ ] The SoD conflict banner must switch between "danger" (conflict) and "success" (clear) styling live as role-holder names change, and must block final submission while a conflict exists.
- [ ] A single-source or emergency declaration must not be treated as an approval — the on-screen copy explicitly states "This is not an approval," and the UI must not imply otherwise.

### Open Questions
- None beyond the general absence of a defined CTO/CFO/SMC/Board approver UI in either source — the S2P document names these gates in prose (M3 business capability), but no screen for CTO/CFO/SMC/Board decisioning is shown in either UI source; only the Procurement-side triage/accept/reject/assign screen and the generic Approval Inbox are shown.

---

## Module: M4 — Sourcing Strategy & Supplier Shortlisting / M5 — Supplier Onboarding & Due Diligence

### Screens / Flows
- **Procurement Triage / Assessment** (prototype, `#procurementView`) — metrics (Budget gate, Award readiness, Approval level), a "Triage Routes" list of system-proposed routes with rationale text, a Governance-status table (Control, Status, System response), a Request-summary definition list, and a decision panel (assign-to-team select, priority select, triage-comments textarea, Request information / Reject-close / Accept-for-assessment actions).
- **Due Diligence** (prototype step) — a system-check panel showing Sourcing Type / Spend Type selects, an explanatory policy banner (Sourceable/Non-Sourceable/Addressable/Non-Addressable definitions), an "Applicable rule" callout, and an assessment grid of 3PC / ESG / Associated Person / TPRM tiles with status badges.
- **Supplier** step's existing-supplier and new-supplier panels — see M1 mapping table above (shared step).

### Components & Interactions
- "Workload prioritisation" side panel (business urgency, governance complexity, submission completeness progress bars) recalculates from `riskLevel()`, `blockingIssues()` and completion percentage.
- Triage routes and governance-status rows are computed, not manually entered, from the same rule engine driving the requestor-side routing panel (`getRoutes()`, `governanceStatuses()`).

### Source Element Mapping
| Prototype element | Component name | Props / State | Triggered action |
|---|---|---|---|
| `#triageRoutes` | `TriageRouteList` | `routes: [name, rationale][]` from `getRoutes()` | display-only |
| `#governanceRows` (`<tbody>`) | `GovernanceStatusTable` | rows from `governanceStatuses()`: control, status badge, system response | display-only |
| `#triageSummary` (`<dl>`) | `RequestSummaryList` | key/value pairs (title, requestor, category, value, route, approval level, supplier, ESG, TPRM, contract action) | display-only |
| `#prioritySelect` | `PrioritySelect` | Standard / High / Critical | shows a message noting the production system must record the reason and audit history |
| `#acceptButton` | `AcceptForAssessmentButton` | disabled-equivalent via `blockingIssues()` check | shows accepted/blocked message |
| `#sourcingTypeInput` / `#spendTypeInput` | `DueDiligenceApplicabilityForm` | Sourceable/Non-Sourceable; Addressable/Non-Addressable Spend | recomputes 3PC/ESG applicability and the "Applicable rule" text |
| `#threePCInput`, `#esgScoreInput`, `#associatedPersonInput`, `#tprmInput` | `DueDiligenceAssessmentTiles` | status/score per check | recomputes `dueDiligenceReady()` badge |
| `#systemAccessInput` / `#dataAccessInput` checkboxes | `MaterialityFlags` | boolean | feed `materialityStatus()` |

### UX Behavior Checklist
- [ ] The Procurement triage view's "Accept for assessment" action must be blocked (with a clear message) while any blocking issue from intake remains unresolved.
- [ ] The Due Diligence tile badges (3PC, ESG, Associated Person, TPRM) must each independently reflect Required/Not applicable and Ready/Review-required states per the applicability rule, not a single combined status.

### Open Questions
- No screen for the M4 sourcing-strategy preparation (market overview, spend-cube, tail-spend analysis, demand consolidation) or the M5 supplier onboarding self-service portal is present in either UI source; only the intake-time supplier-status/due-diligence check screens above are shown.

---

## Module: M6 — RFx Design, Event Administration & Submission

### Screens / Flows
- **RFx Events** (`frontend/src/features/rfx-events/RfxEvents.tsx`, built M14-1/M14-2, no reference mockup existed for this module — designed fresh against `business_kb.md`'s M6 Feature Trace and `technical_kb.md`'s M6 REST API listing, reusing the design-system.md card/table/badge/filter patterns) — a master-detail screen: a filterable RFx Events list (status filter: draft/published/opened/closed/cancelled) with an inline "New RFx Event" create form (Sourcing Strategy ID, Tender Type, Technical/Commercial Template ID), and a detail panel for the selected event showing its metrics (tender type, opening/closing dates, invited-supplier count), a Publish action (enabled only while `draft`), an Invite Suppliers action (comma-separated supplier IDs — no supplier directory/search endpoint exists yet to build a real picker against), an Extend Deadline action (new closing date + mandatory reason), a Submissions table, and an Open Proposals action (enabled only once published and not yet opened).

### Components & Interactions
- Row-click select pattern on the RFx Events table sets the detail panel below it — no separate route/URL per event (matches this app's existing single-page `View` state pattern in `App.tsx`, not a router).
- Publish/Open Proposals buttons are `disabled` client-side based on `status`, mirroring (not replacing) the backend's own gating (422 on incomplete-template publish, 403 on opening before the deadline) — the disabled state is a UX nicety, the backend remains the enforcement point.
- **Backend deviation**: `technical_kb.md`'s M6 REST API Listing had no `GET` endpoint at all (only the write actions) — a screen cannot let a buyer pick an RFx event to act on without one. Added `GET /api/v1/rfx-events` (paged list, `status` filter) and `GET /api/v1/rfx-events/{id}` (detail, including invited supplier IDs and submissions) to `RfxEventsController.cs`, documented here and in the M14 Jira epic rather than silently invented.

### Source Element Mapping
- No prototype/reference markup exists for this screen (see Open Questions below) — the frontend component names are original: `RfxEvents` (page), inline create form, detail panel, submissions table.

### UX Behavior Checklist
- [ ] Publish must be blocked (and is, both client-side via `disabled` and server-side via 422) until both technical and commercial template IDs are set.
- [ ] Open Proposals must be blocked until the RFx event is `published` and not already `opened`.
- [ ] Extend Deadline must require a non-empty reason, matching the backend's 422 on a missing reason.

### Open Questions
- No reference mockup ever existed for this screen (carried forward from the prior version of this section) — the M14 implementation above is an original design, not a transcription of a source; a real supplier-directory/search endpoint would let Invite Suppliers become a proper picker instead of a raw ID input.

---

## Module: M7 — Technical, Commercial & Contract Evaluation

### Screens / Flows
- **Evaluation Workspace** (`frontend/src/features/evaluation-workspace/EvaluationWorkspace.tsx`, built M15, no reference mockup existed — designed against `business_kb.md`'s M7 Feature Trace and `technical_kb.md`'s M7 REST API listing) — select an RFx event (reusing the same event-list pattern as M14's RFx Events screen), then: a Score Evaluation form (Supplier ID, Evaluator ID, Technical/Commercial scores, comments, Lock toggle) plus a table of existing evaluations; a Clarifications section (raise with category/question/material-deviation flag, resolve pending ones inline with a response field) plus a table with Resolve actions; and a Status Gate Decision panel (proceed/return/reject).

### Components & Interactions
- The Lock toggle on Score Evaluation mirrors the backend's own lock semantics (`POST .../evaluations` 409s once `status: locked`) — once locked, re-submitting the same supplier+evaluator pair is expected to fail, surfaced via the same danger `StatusMessage` pattern used across the app.
- A clarification flagged "material deviation" gets a `warning` badge; the Status Gate Decision's "proceed" option is expected to fail (422) while any material deviation on the selected event remains unresolved, matching the backend's own gate.

### Source Element Mapping
- No prototype/reference markup exists for this screen — original component names: `EvaluationWorkspace` (page), inline score/clarification forms, resolve-inline table pattern.

### UX Behavior Checklist
- [ ] Re-scoring a locked evaluation (same supplier + evaluator) must surface the backend's 409 as a danger `StatusMessage`, not fail silently.
- [ ] A material-deviation clarification must be visually distinguished (warning badge) from a routine one.
- [ ] "Proceed" status-gate decisions must reflect the backend's pass/fail outcome directly (`passed`/`message` from the response), not assume success.

### Open Questions
- No reference mockup ever existed for this screen; the M15 implementation above is an original design, not a transcription of a source.

---

## Modules M8 — Negotiation/eAuction, M9 — Award & Contract Execution, M12 — Post-Contract Management

### Screens / Flows
No screens for negotiation/eAuction, award-recommendation authoring, contract drafting/signing, or post-contract supplier management/SRM are present in either UI source, and none has been designed yet (tracked as Jira epics M16/M17/M20 — see M14/M15's precedent above for the pattern: no source screen exists, design fresh against business_kb.md + technical_kb.md, reuse design-system.md). The prototype's **Lifecycle Controls** view (`#lifecycleView`) shows a read-only 10-card control map (one card per S2P stage: Intake & budget, Supplier sourcing, Supplier due diligence, RFQ/RFP release, Tender submission, Evaluation, Award & approval, PO/LOA/LOI, Contract & variation, Performance & archive — each listing its enforced controls as bullet text) plus a read-only workflow-tracking timeline (Request, Budget, Assessment, Due diligence, RFQ/RFP, Tender, Evaluation, Award, PO/LOA/LOI, Contract stages with status dots done/current/blocked). This is explicitly described in the prototype as illustrative of downstream controls, not an interactive screen for those modules.

### Components & Interactions
- `.life-grid` control cards (`#lifecycleGrid`) — one per stage, static bullet list, "Control set" badge.
- `.timeline` status cards (`#lifecycleTimeline`) — status dot (done/current/blocked), stage name, current value, detail line.

### Source Element Mapping
| Prototype element | Component name | Props / State | Triggered action |
|---|---|---|---|
| `#lifecycleGrid` | `LifecycleControlGrid` | `lifecycleControls: [stageName, controls[]][]` (10 fixed entries) | display-only |
| `#lifecycleTimeline` | `WorkflowTrackingTimeline` | `trackingStages()` output: `[stage, statusDotClass, headline, detail][]` | display-only |

### UX Behavior Checklist
- [ ] The lifecycle/timeline view is read-only in the prototype; the UX Behavior Checklist for the real M8/M9/M12 screens cannot be derived from either source and must be defined when those screens are designed.

### Open Questions
- Screens for negotiation/eAuction, award-recommendation review, contract drafting/e-signature, contract catalogue/pricebook management, and post-contract SRM/KPI dashboards are not described in either UI source and are open for UI/UX design (Jira: M16, M17, M20).

---

## Module: M10 — PR/PO, Delivery & Invoice 3-Way Match / M11 — Payment, Self-Billed e-Invoice, GL & Bank Reconciliation

### Screens / Flows
- **Workspace — Requisitions** (Finance Portal, existing) — see M1 mapping; "Headcount Hiring Request" list with Status/Category filters and a table (Ref, Title, Category, Role, HC, Est. Cost, Reserved, Status, Action).
- **Finance — Requisition Finalisation** (Finance Portal, existing) — a "Finance Commitment Console" with a "Ready to Finalise" section (one card per requisition awaiting finalisation: reference, status, linked spend-paper/category, "CAPEX/OPEX commitment," Total Estimated Cost, View/Release/Finalise actions) and an "All Finalised Requisitions" searchable, paginated table (Reference, Title, Final Spent, Finalised On, Notes).
- **Finance — Finance System home** (Finance Portal, existing) — a dashboard combining Budget Health metrics, Spend Trend chart, "Pending My Approval" table (Title, Submitter, Amount, SLA) and a "Finance Console" requisition table (Requisition, Type, Total Cost) plus Cost Centre utilisation and "Top 5 by Spend" panels.
- **Finance — Payment Vouchers** (named nav item only; screen content not captured in the reference screenshots).

### Components & Interactions
- Ready-to-finalise cards show a status chip ("awaiting finalisation"), a linked spend-paper reference, a commitment-type chip ("CAPEX commitment"/"OPEX commitment") and a Total Estimated Cost figure, with Release and Finalise as the primary actions.
- The finalised-requisitions table supports free-text search (placeholder "Reference, title, or hire name…"), pagination (page size 20/50/100) and shows "—" for an empty Notes cell.

### Source Element Mapping
- Both Requisitions/Requisition-Finalisation screens are captured only as rendered Finance Portal snapshots (no literal prototype markup with IDs), so no `Prototype element → Component` table is produced for them — they reuse the shared Finance Portal table/filter/pagination pattern documented under Design System.

### UX Behavior Checklist
- [ ] The Requisition Finalisation "Release" and "Finalise" actions must only be available on requisitions in the "awaiting finalisation" status shown in the Ready-to-Finalise section.
- [ ] The finalised-requisitions table's search must filter by reference, title, or hire name per its placeholder text.

### Open Questions
- No screen for supplier e-Invoice/MyInvois capture, ePV voucher review, duplicate-invoice/3-way-match exception handling, or the Payment Vouchers/Vendors/Fin Ops Config/Reports/System Settings screens' actual content is present in either UI source — only their navigation entry points are shown.
