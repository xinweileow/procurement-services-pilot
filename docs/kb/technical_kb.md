<!-- Sketch only — fsd-writer.md enriches this into full feature-trace/API/data-model detail once business_kb.md and ui_ux.md are committed. -->

# Technical Knowledge Base (sketch) — Etiqa Procurement System

## Services

- **Finance Portal** — the existing one-stop UI/role authority (Workspace: Dashboard, Requisitions, Approval Inbox; Finance: Finance System home, Budget, Requisition Finalisation, Payment Vouchers, Vendors, Fin Ops Config, Reports, System Settings, Admin Console/Settings/Audit Log).
- **Procurement Service** — owner of PR, sourcing, RFQ/RFP, supplier screening, evaluation, award and PO records (covers M1, M4–M10 process logic).
- **Budgeting / Finance Budgeting (BM)** — owns the annual budget baseline, availability checks, commitment/encumbrance and budget exception workflow (M2).
- **Approval / Governance service** — owns CTO/CFO/SMC/Board gates, dynamic multi-level approval routing, segregation-of-duties checks and approval audit trail (M3).
- **Supplier / Vendor Management** — owns supplier registration, due diligence, risk scoring, ESG, categorisation, self-service portal and lifecycle (M5, M12).
- **RFx / Sourcing Event service** — owns RFx templates, event administration, evaluation workspace, clarification/deviation tracking and negotiation/eAuction (M6–M8).
- **Contract Management** — owns contract authoring/versioning, digital signing/LOA enforcement, central repository, catalogue/pricebook and AGMT ID issuance (M9).
- **ePV (electronic Payment Voucher)** — owner of voucher auto-creation, duplicate-invoice check, 3-way match and approval (M10).
- **Payout** — owner of payment readiness, execution and status (M11).
- **MyInvois / Self-Billed e-Invoice** — supplier e-Invoice capture and buyer-issued self-billed submission/response handling (M10, M11).
- **GL / Bank Reconciliation** — GL posting and bank statement matching/AP clearing (M11).
- **CCM (evidence/records retention)** — audit-pack assembly and long-term evidence retention (M11, M12).
- **Kafka** — the event backbone connecting the above services (named in the source as the target integration approach).
- **ACP / SAS** — legacy file-based downstream consumers (named in the source, role otherwise unspecified).

## Data Model (sketch)

- **Request** — Request ID, Requester, Business Unit, Cost Centre, Category, Estimated Value, Currency, Requirement/Specification, Business Justification, Budget Reference, Engagement Pathway, Procurement Nature, Status, Submission Date, Owner.
- **Budget** — Fiscal Year, Entity, Business Unit, Cost Centre, GL Account, Project Code, Currency, Budget Amount, Committed Amount, Actual Spend, Available Balance.
- **Approval** — Approver ID/Name, Role, Approval Level, Cost Centre/BU Scope, Category Scope, Spend Threshold Min/Max, Effective/Expiry Date, Delegation Chain, Override Flag, Policy Version, Decision, Comments, Timestamp.
- **Supplier** — Supplier ID, Legal Entity Name, Registration Number, Tax ID, Address, Contact Details, Bank Details, Payment Terms, Certifications, KYC Status, ESG Score, Category Codes/UNSPSC, Spend Tier, Active Flag.
- **RFx Event** — RFx ID, Bid Reference, Supplier ID, Technical Proposal, Commercial Proposal, Unit Pricing, Cost Breakdown, Currency, Bid Status, Submission Timestamp, Clarification Q&A, Attachments.
- **Contract / AGMT** — Contract ID, Supplier ID, Version Number, Approval Status, Digital Signature, Execution Date, Effective Dates, Contract Status, Amendment Flag, Redlined Document, Comment Threads, Clause Amendments, Counterparty Details.
- **PR / PO** — PR Number, PO Number, Supplier ID, Contract ID, Cost Centre, GL Account, Item, Quantity, Amount, Currency, Approval Level, PO Date.
- **Delivery / GR** — PO Number, Delivery Reference, GRN, Quantity Received, Acceptance Date, Accepted By.
- **Invoice** — Invoice Number, Invoice Date, Supplier ID, PO, GRN, Amount, Tax, MyInvois Reference/QR/Status, Invoice Type.
- **ePV Voucher** — Voucher ID, PO, GRN, Invoice Number, Supplier ID, Amount, Tax, Match Status, Approval Status, Payment Route.
- **Payment** — PV/EPRF/BOT Number, Voucher ID, Supplier ID, Amount, Currency, Payment Date, Payment Type, Bank Reference, Payment Status.
- **Self-Billed Record** — Payment ID, Supplier ID, Tax ID, Amount, Tax, MyInvois Payload, Submission Date, Unique ID/QR/Status, Rejection Reason.
- **GL Entry** — GL Account, Cost Centre, Entity, Debit, Credit, Tax, WHT, Posting Date, Reversal Flag.
- **Bank Record** — Bank Reference, Payment ID, Date, Amount, Currency, Status, Match Status, AP Clearing Status.
- **Exception (generic)** — Exception ID, Exception Type, Owner, Reason, Ageing, Resolution, Adjustment Note.
- **Dispute** — Dispute ID, PO Number, GRN Number, Supplier ID, Dispute Type, Description, Supporting Evidence, Resolution Status, Escalation Flag, Resolution Date, Contract ID.

## API Contracts (stubs)

No literal wire-level API definitions are stated by the source; the source only names the data/objects exchanged between process steps and, for the eProcurement prototype, client-side mock/synthetic state fields (e.g. `state.budget`, `state.supplierMode`) that never call a real backend. All endpoint shapes below are placeholders pending `fsd-writer` synthesis:
- Demand intake submission (M1) — mock/synthetic in the prototype (in-browser state only, no network call).
- Budget availability check (M2) — referenced conceptually ("system compares the requested amount with the latest available budget") but no literal request/response shape stated.
- Supplier registration/duplicate-check (M5) — referenced conceptually, no literal shape stated.
- RFx publish/submit (M6) — referenced conceptually, no literal shape stated.
- 3-way match (M10) and payment execution (M11) — referenced conceptually, no literal shape stated.

## Open Decisions (TBD)

- Which specific system features are "Existing ETQ" (already built) vs. "Recommendation"/"Optional Future" (net-new, benchmarked against Maybank) is not tagged per-feature in the source; needs confirmation with the Etiqa process owner before BRD sign-off (see business_kb.md's cross-module Open TBDs).
- The source's own priority roadmap groups capabilities into P0 (Phase 1: intake/approval traceability, supplier onboarding + duplicate/bank/risk controls, RFx templates + evaluator controls + clarification/deviation, contract lifecycle + pricebook linkage), P1 (Phase 2: negotiation/BAFO/eAuction, award scenario + savings tracking, supplier performance/SRM/demand consumption) and P2 (Phase 2/3: market intelligence/spend cube/should-cost/price variance; Phase 3: AI-guided buying/anomaly detection, explicitly requiring human approval to remain mandatory) — this phasing is not yet confirmed as the accepted build sequence.
- Whether the target build extends the existing Finance Portal codebase/screens directly or is a new system that must integrate with it (event backbone: Kafka; legacy consumers: ACP/SAS) is not stated.
- No literal REST/API contract, authentication scheme, versioning convention, or error-response shape is stated anywhere in the source material for any module.
