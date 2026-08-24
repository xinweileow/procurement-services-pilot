# Business Knowledge Base — Etiqa Procurement System (Source-to-Pay / S2P)

Sources indexed: "ETIQA PROCUREMENT SYSTEM — Detailed End-to-End Process, Workflow Diagram & Functional Requirement Baseline" (S2P Flow document, P1–P12 process baseline enhanced against the Maybank S2P benchmark), the "Etiqa Smart Procurement Intake & Governance Prototype" (eProcurement prototype HTML, business-user and procurement-triage intake experience), and the existing Etiqa Finance Portal (Workspace and Finance console screens: Dashboard, Requisitions, Approval Inbox, Budget, Requisition Finalisation) used as the current-state UI/technical reference. No prompt-injection lines were flagged by `sanitize-doc` in any source.

## Modules Overview

- **M1 — Demand Intake & Business Case**: capture, validate and track a procurement/hiring request from business need through submission, including the catalogue-first and role-based guided intake experience. Depends on: none.
- **M2 — Budgeting & Budget Control**: annual budget baseline, availability checks, exception approval, commitment reservation and budget visibility. Depends on: M1 (a request must exist before its budget is checked).
- **M3 — Approval & Governance Gate**: CTO/CFO/SMC/Board approval gates, segregation-of-duties enforcement, dynamic multi-level approval routing and approval audit trail. Depends on: M1, M2.
- **M4 — Sourcing Strategy & Supplier Shortlisting**: sourcing-method selection (RFQ/RFP/Single Source/Renewal), vendor 3-point screening, sourcing strategy preparation, market/spend/tail-spend analysis and demand consolidation. Depends on: M3.
- **M5 — Supplier Onboarding, Due Diligence & Lifecycle Management**: supplier registration and self-service portal, duplicate/company/tax/bank checks, financial health, risk scoring, beneficial ownership, ESG, categorisation, document-expiry tracking, self-service updates, deactivation and re-qualification. Depends on: M4 (a supplier must be shortlisted or proposed before onboarding/DD applies), can also run in parallel with M1 for an existing registered supplier.
- **M6 — RFx Design, Event Administration & Submission**: technical/commercial/contract requirement templates, RFx builder, evaluator assignment, RFx configuration validation, vendor invitation, open/invited tender control, publishing/scheduling, extension, late-submission handling, cancellation and sealed proposal opening. Depends on: M4, M5.
- **M7 — Technical, Commercial & Contract Evaluation**: evaluation workspace and scoring, commercial evaluation, contract/legal evaluation, supplier clarification workspace, deviation tracking, structured vendor communication, change control and scoring-anomaly detection. Depends on: M6.
- **M8 — Negotiation, eAuction, BAFO & Consolidation**: negotiation/eAuction rounds, revised/BAFO offers, contract commercial-term negotiation, evaluation consolidation, award-scenario comparison, savings/cost-avoidance calculation and price-variance comparison. Depends on: M7.
- **M9 — Award, Due Diligence & Contract Execution**: award recommendation and approval, final supplier due diligence (financial, ESG, compliance), contract lifecycle management, central contract repository, contract authoring/versioning, digital signing with LOA enforcement, contract catalogue/pricebook and AGMT ID issuance. Depends on: M8.
- **M10 — PR/PO, Delivery & Supplier e-Invoice 3-Way Match**: PR creation from an approved award/contract/AGMT ID, catalogue/non-catalogue/manual PO routing, PR approval and PO issue, contract/price validity check, delivery/GR/service acceptance, invoice-treatment decision, supplier e-Invoice/MyInvois capture, ePV voucher auto-creation, duplicate-invoice check, 3-way match and mismatch resolution. Depends on: M9 (for sourced/contracted spend); can also start directly from an approved M1–M3 request for non-sourced, catalogue or below-threshold spend.
- **M11 — Payment, Self-Billed e-Invoice, GL Posting & Bank Reconciliation**: payment-route selection, PV/EPRF/BOT preparation, tax/WHT/SST treatment, payment approval with SoD, beneficiary/duplicate/limit checks, Payout execution, self-billed MyInvois applicability/submission/response handling, end-to-end reference linking, GL posting, bank statement intake, bank reconciliation/AP clearing and audit-pack retention in CCM. Depends on: M10.
- **M12 — Post-Contract Supplier Management, Savings & Audit**: contract obligation/renewal tracking, supplier market intelligence, commercial variation/dispute management, operational/technical performance monitoring, KPI/SLA tracking, demand/consumption monitoring, supplier relationship management (SRM) segmentation, savings calculation with Finance validation, supplier re-qualification and end-to-end P2P audit pack/dashboard. Depends on: M9 (contract must exist), M11 (for savings validated against actual spend).

Cross-module role model (from the eProcurement prototype's role selector): **IT Business Requestor** and **Non-IT Business Requestor** (both use the M1 catalogue-first/guided-intake flow, restricted to their own category set) and **Procurement Administrator** (uses the M1 triage/overview flow plus M3/M4/M5 governance, due-diligence, people/duties and document-review screens). The prototype states menus, tabs and category options change based on the selected role.

---

## Module: M1 — Demand Intake & Business Case

### Capabilities

#### Feature: Guided Demand Intake & Business Case Capture
**User Story:** As a Requester / BU, I want to describe the business need, scope, justification and budget reference in a guided intake form, so that Procurement can identify the request owner and start the correct intake route.
**Requirements Checklist:**
- [ ] Provide a demand intake screen capturing business need, expected outcome, estimated value, category and benefiting business unit; save as draft until minimum information is complete; keep requester and submission history.
- [ ] Allow the requester to enter item/service scope, specification, quantity, delivery date, technical requirements and required attachments as the approved requirement baseline for later sourcing/evaluation/contract steps.
- [ ] Capture business justification, expected benefit, project objective, estimated cost, available options and budget reference, attached to the request for reviewer context.
- [ ] Provide a guided request form that checks category, value, cost centre, budget information, requirement and supporting documents are entered before submission; lock the submitted request as a controlled version and route it based on request type, value, category, supplier and procurement scope.
- [ ] Generate a unique Request ID on submission, recording submission date, requester, owner and current status; carry the same Request ID through budget, sourcing, contract, PR, PO, invoice, payment and audit records.
- [ ] Validate mandatory fields, required documents, value, category and routing information before the request proceeds; return incomplete/invalid requests to the requester with missing items clearly shown.
- [ ] Allow Procurement to review the submitted requirement, category, estimated value, supplier information and documents, and to accept, return for clarification, reject with a reason, or assign to a buyer/category manager.
- [ ] Compare the new request against existing/historical requests by requester, category, supplier, description, timing and estimated value, and flag possible duplicates or purchase-splitting for Procurement review before the request proceeds.
- [ ] Present an "Engagement pathway" classification (Sourcing with Contract, Sourcing Only, Contract Only, Other Query) and a "Procurement nature" classification (New Procurement, Renewal, Variation Order, Contract Extension); for Renewal/Variation Order/Contract Extension, require the previous Procurement/CMT/Contract ID and retrieve the existing contract summary (supplier, original value, start/expiry date, performance rating, previous approver, cumulative tenure) for the requester's review.
- [ ] For a Contract Extension, capture whether the original approval includes an extension clause, whether rates or terms will change, and cumulative tenure in months; determine "Extension eligible" only when the clause exists, rates and terms are unchanged, and cumulative tenure is below 60 months, otherwise require fresh approval or tender.
- [ ] Provide a catalogue-first check before the non-catalogue intake form: let the business requestor search the catalogue by item/service/keyword/supplier/category; if the item is not available, mark the request as Non-Catalogue and enable the non-catalogue intake screens.
- [ ] Restrict the procurement category options shown to a requestor by their role (e.g. IT Business Requestor sees "IT and Telecommunication"; Non-IT Business Requestor sees Facilities Management, Sales and Marketing, General Spend, Professional Services; Procurement Administrator sees all categories), each with a short category description shown to the user.
- [ ] Capture country/operation and, for Malaysia, the specific legal entity (Etiqa, MBB Banking-Maybank, MBB Banking-Maybank Islamic Berhad, Maybank Shared Services, Maybank Investment Bank Berhad, Other Malaysia Entity); for non-Malaysia countries capture a free-text Entity/OU.
- [ ] Capture business criticality (Standard, Important, Business Critical, Regulatory / Safety Critical) and required delivery/commencement date as mandatory fields.
- [ ] Require mandatory evidence uploads at intake (approved business case, scope/specification, existing contract for renewal/variation/extension) with a file-size limit (20 MB per file in the prototype) and show required/optional/attached status per document type.
- [ ] Validate, before submission, that the title, business justification, category, delivery date, budget reference and amount are present and that four compliance declarations (no conflict of interest / all conflicts declared, connected-party declaration, no-splitting declaration, complete-and-accurate declaration) are checked; block submission and show the first unmet condition otherwise.
- [ ] On submission, determine and display the routing destination based on value: below the Malaysia RM100,000 threshold routes to "Etiqa Internal Procurement Team"; RM100,000 and above routes to "GSP for further assessment" (Group Strategic Procurement / Strategic Procurement unit re-routing), and show the corresponding confirmation message to the requester.
- [ ] Provide a Procurement-side triage/overview screen summarising the submitted request, routing destination, budget gate, due-diligence status and system-proposed routes, with actions to return the request to the requestor for missing information, accept it for assessment, assign a work owner (GSP/Strategic Procurement, Etiqa Internal Procurement Team, Risk/TPRM Team, Legal/Contract Team, Supplier Registration Team) and view the audit trail.
**Depends on:** none

### Stakeholders / Governance
- Requester / Business Unit (BU) — describes the need, requirement, justification and submits the request.
- Procurement — reviews, accepts/returns/rejects/assigns the request; performs triage.
- System — generates Request ID, validates mandatory data, runs duplicate/anti-splitting detection, applies role-based category and routing rules.
- Roles observed in the prototype: IT Business Requestor, Non-IT Business Requestor, Procurement Administrator — each with a distinct step set and allowed category list.

### Process Flow
1. Requester identifies the business need, defines the requirement/specification and captures the business justification.
2. Requester searches the catalogue first; if unavailable, the request is marked Non-Catalogue and the guided intake form is used.
3. Requester classifies the engagement pathway and procurement nature, and (for renewal/variation/extension) confirms the retrieved prior contract details and extension eligibility.
4. Requester submits the completed, validated request; the system generates a Request ID and routes it (Etiqa Internal Procurement Team, or GSP for RM100,000 and above in Malaysia).
5. Procurement reviews the request, checks for duplicates/possible splitting, and accepts, returns, rejects or assigns it; accepted requests proceed to Budget Check (M2).

### Open TBDs
- Process ownership, approval thresholds, system-of-record boundaries and implementation priority must be validated with the relevant Etiqa process owner before BRD sign-off (stated as a blanket caveat covering all modules in the source; repeated per-module below where the source restates it).
- The current Etiqa playbook baseline starts from demand intake and approved workflow records; the source states the detailed requirement-discovery, RFI, business-case and policy-based request-validation layers described here are enhancements relative to that baseline, not confirmed as already built.

---

## Module: M2 — Budgeting & Budget Control

### Capabilities

#### Feature: Budget Availability, Commitment & Exception Control
**User Story:** As Finance / Budget Owner, I want the system to validate and reserve budget against every request in real time, so that spend never exceeds the approved baseline without an authorised exception.
**Requirements Checklist:**
- [ ] Store the approved annual budget by entity, business unit, cost centre, GL account, project, fiscal year and currency as the reference baseline for availability checks, commitments, actual spend and reporting.
- [ ] Compare the requested amount with the latest available budget for the selected cost centre, GL account, project and fiscal year; continue the request if sufficient, otherwise block normal processing and start the budget-exception route.
- [ ] When budget is insufficient/unavailable, create an exception request showing the shortfall, reason and justification; route it per the active Delegation of Authority (DOA) policy, record every approval/rejection, and allow the request to proceed only after the required approval.
- [ ] After approval, reserve (encumber) the approved amount against the relevant budget to prevent double-use; update or release the commitment when the PR, PO, invoice or cancellation changes the required amount.
- [ ] Show approved budget, committed amount, actual spend and remaining balance by category, entity, business unit and cost centre so Finance/Procurement can monitor overspending, upcoming commitments and categories needing action.
- [ ] When a PR is submitted, perform a fresh budget check against the latest balance rather than relying on the earlier demand-stage result; stop the PR for correction or budget-exception approval if no longer sufficient.
- [ ] Provide a Budget Console showing allocated/reserved/actual-spend/available budget with CAPEX/OPEX split, a yearly expenses breakdown by fiscal year/month/cost type, and a cost-centre breakdown table (capex balance, opex balance, utilisation %), matching the existing Finance Portal Budget screen.
- [ ] Compute utilisation percentage as (Reserved + Actual Spend) ÷ Allocated Budget × 100 and display it alongside allocated, reserved, available and spent amounts.
- [ ] Support a "Requisition Finalisation" console where Finance releases or finalises a requisition's commitment once actual spend is known, recording final spent amount, finalisation date and notes, matching the existing Finance Portal Requisition Finalisation screen (Reference, Title, Final Spent, Finalised On, Notes columns; Release / Finalise actions).
- [ ] Support an "RFI only" flag allowing a request to proceed for market information/quotation purposes before budget approval, while keeping formal RFQ/RFP issuance blocked until budget is approved.
**Depends on:** M1

### Stakeholders / Governance
- Finance / Budget Owner — sets and owns the annual budget baseline.
- Finance / System — runs availability checks and revalidation.
- CFO / SMC / Board — approve budget exceptions per the active DOA (see M3).
- Procurement — monitors category budget visibility alongside Finance.

### Process Flow
1. Finance sets up the approved annual budget baseline by entity/BU/cost centre/GL account/project/fiscal year/currency.
2. On request submission (M1) and again at PR submission (M10), the system checks the requested amount against the latest available budget.
3. If insufficient, a budget-exception request is created and routed per DOA; the request is blocked until approved.
4. Once available/approved, the system reserves (encumbers) the amount; the reservation is updated or released as the PR/PO/invoice progresses or the request is cancelled.
5. Finance and Procurement monitor category/cost-centre budget utilisation on an ongoing basis via the Budget Console.

### Open TBDs
- Exact ownership, thresholds and exception-ageing rules for the budget-exception workflow must be validated with the relevant Etiqa process owner before BRD sign-off.

---

## Module: M3 — Approval & Governance Gate

### Capabilities

#### Feature: Multi-Level Approval Routing & Segregation of Duties
**User Story:** As a Governance/Approval stakeholder, I want the system to route each request to the correct approval gate (CTO/CFO/SMC/Board) using the active policy, and to prevent one person holding conflicting roles, so that every commitment has traceable, non-conflicted authorisation.
**Requirements Checklist:**
- [ ] Check the active approval policy to determine whether CTO approval is required (based on request type, technology scope, value, entity, risk) and route to the authorised CTO approver if so, otherwise proceed to the next gate.
- [ ] Check whether CFO approval is required (value threshold, budget status, entity, category, exception type), route to the correct CFO-level approver, and record decision, comments, delegation and approval time.
- [ ] Identify requests requiring SMC endorsement (value, strategic importance, policy requirement, exception status), send the complete approval pack to the designated SMC route, and record endorsement, rejection, or a request for more information.
- [ ] Check whether the request exceeds the Board/ACB approval threshold or is a Board-controlled matter; block progression until the Board decision and supporting evidence are recorded.
- [ ] Prevent the same person from requesting, approving, preparing and releasing the same transaction (segregation of duties); block the action, route to another authorised person and record the conflict for audit when detected.
- [ ] Calculate the approval route dynamically from entity, business unit, category, value, risk, role, delegation and current policy version; send the task only to an active authorised approver, and automatically escalate/reassign when the approver is unavailable or delegation has expired.
- [ ] Retain, for every approval step, the request version, approver, decision, date/time, comments, delegation, override and policy version as immutable evidence for audit, investigation and management review.
- [ ] Derive an "Approval level" indicator by value banding (e.g. Department + Procurement below RM500,000; ETC/GPPC from RM500,000; GPPC/EXCO from RM1,000,000; EXCO from RM3,000,000; Board from RM5,000,000, per the prototype's illustrative banding) and surface it to both the requestor and Procurement Administrator views.
- [ ] Detect and surface a segregation-of-duties conflict specifically among the Procurement lead, technical evaluator, commercial evaluator and approver roles captured on a request (People & Duties step), blocking progress with a clear "conflict detected" indicator until distinct users are assigned.
**Depends on:** M1, M2

### Stakeholders / Governance
- CTO, CFO, SMC, Board/ACB — approval authorities at successive gates.
- System / Governance — enforces SoD and calculates the dynamic approval route.
- Procurement / Approver — receives and actions the routed approval task.

### Process Flow
1. The system evaluates, in sequence, whether CTO, CFO, SMC and Board approval are each required for the request, based on the active policy.
2. For each required gate, the system routes to the correct authorised approver, applying delegation/escalation rules if the approver is unavailable.
3. Before/alongside routing, the system checks for segregation-of-duties conflicts across requester/approver/preparer/payer (and, at People & Duties intake, across procurement lead/technical evaluator/commercial evaluator/approver) and blocks progress on conflict.
4. Every decision (approve/reject/delegate/override) is recorded immutably with policy version, approver identity and timestamp for audit.

### Open TBDs
- Exact ownership, thresholds, exception ageing and system-of-record boundaries for each approval gate must be validated with the relevant Etiqa process owner before BRD sign-off.

---

## Module: M4 — Sourcing Strategy & Supplier Shortlisting

### Capabilities

#### Feature: Sourcing Route Selection & Category Strategy
**User Story:** As Procurement (Category Manager), I want to select the correct sourcing route and build a sourcing strategy grounded in market, spend and demand analysis, so that the RFx event created afterward reflects an agreed, defensible approach.
**Requirements Checklist:**
- [ ] Recommend the sourcing route (RFQ, RFP, Single Source, Renewal) using requirement, value, complexity, supplier availability and contract status: RFQ for clear requirements where suppliers compete mainly on price; RFP for complex requirements needing proposed solutions/approaches; Single Source only with documented justification and approval; Renewal subject to supplier performance, price, budget and approval checks.
- [ ] Check whether the proposed supplier is registered, active and eligible for the relevant category via a three-point screening (identity, required registration/compliance evidence, active status); unresolved/failed checks must be closed before the supplier is included in sourcing.
- [ ] Provide a sourcing-strategy record covering the selected route, market approach, supplier list, evaluation method, timeline, commercial baseline and key risks, reviewed and agreed by Procurement and the requester before the RFx event is created.
- [ ] Bring together supplier, category, risk, location, capability and historical-participation information so the category manager can assess market options and decide whether wider competition or extra due diligence is needed.
- [ ] Compare historical spend, previous purchase prices, contract prices, supplier quotations and available benchmark prices for the same/similar category, calculate price differences and highlight unusual increases to set a realistic negotiation baseline.
- [ ] Identify requests with similar category, item, timing, location or supplier requirements so Procurement can combine suitable demand into one sourcing event.
- [ ] Identify low-value, high-frequency purchases and fragmented spend across users/suppliers/BUs to support consolidation, catalogue coverage, contract coverage or another buying channel decision.
- [ ] Record the final agreed sourcing route, supplier approach, evaluation model, timeline, approval requirement and requester confirmation; an RFx event can be created only after the sourcing strategy is approved or an authorised exception is recorded.
- [ ] Apply the country-specific minimum-quotation rule from the prototype: below the applicable threshold requires a minimum of 1 quotation; at or above the threshold requires a minimum of 3 quotations and triggers the tender route (thresholds: Malaysia RM100,000; Singapore RM10,000; Thailand RM10,000; Cambodia/Indonesia/Other Countries RM5,000 — currency shown as the request's selected currency).
- [ ] Route category "Single Source / Exception" when the requestor flags a single-source scenario, "Emergency Procurement / Single Quote Exception" when flagged emergency, and "Tender / Competitive Sourcing (Minimum 3 Quotations)" otherwise when at/above threshold; require a valid, validated amount before any route is determined.
- [ ] Require, for a single-source request, a single-source category (Proprietary Solution, Regulatory Requirement, Sole Supplier, Confidential Engagement, Maintenance Continuity, Other), a written justification and supporting evidence before the request can proceed.
- [ ] Require, for an emergency procurement request, a justification, disruption-impact description and approving-authority evidence, and require the supplier to be an existing registered supplier unless an approved exception is recorded.
- [ ] Detect potential purchase-splitting against related requests (same/similar department, supplier, category, submitted within a defined recent window) and require Procurement review and written justification before the request can proceed when flagged.
**Depends on:** M3

### Stakeholders / Governance
- Procurement (Category Manager / Buyer) — selects sourcing route, prepares and agrees strategy.
- Requester / BU — agrees the sourcing strategy for their request.
- Procurement / Vendor — performs 3-point vendor screening.

### Process Flow
1. Procurement selects the sourcing method based on requirement clarity, value, complexity and contract status.
2. Procurement screens the proposed/shortlisted vendor(s) for registration and eligibility.
3. Procurement reviews market, historical spend/pricing, consolidation opportunities and tail-spend classification to build the sourcing strategy.
4. Procurement and the requester agree the final sourcing strategy; an approved strategy (or documented exception for single-source/emergency) is required before an RFx event (M6) can be created, or before proceeding directly to award for single-source/renewal routes.

### Open TBDs
- Maybank's category-management layer (market overview, price trends, should-cost intelligence, ESG components, requirement build-up, spend analysis) is described as a benchmark; the source flags these as important planning capabilities beyond the basic RFQ/RFP route without confirming which are Day-1 for Etiqa (see Priority Roadmap in the Technical KB / Open Decisions).

---

## Module: M5 — Supplier Onboarding, Due Diligence & Lifecycle Management

### Capabilities

#### Feature: Supplier Registration, Screening & Risk Scoring
**User Story:** As Procurement / Vendor Admin, I want a controlled supplier registration and due-diligence process, so that only approved, active, risk-assessed suppliers are available for sourcing and payment.
**Requirements Checklist:**
- [ ] Capture supplier legal name, registration number, tax information, address, contacts, bank details, payment terms, category and required documents; keep a supplier record pending until required checks/approvals are complete, and only an approved active supplier is available for sourcing/payment.
- [ ] Provide a supplier self-service registration portal that guides the supplier through registration, shows incomplete items, saves drafts, records submission dates and allows Procurement/Vendor Admin to return the application for correction.
- [ ] Display the correct registration fields and document checklist based on supplier country, category, legal form, risk and expected spend; mandatory fields cannot be skipped and submission is blocked until required documents are attached.
- [ ] Compare a proposed supplier against existing records using legal name, registration number, tax ID, bank account, address and contact details; block or route possible duplicates to Vendor Admin for review before a new supplier master/record is created.
- [ ] Record company registration/licence, issuing authority, effective and expiry dates; Vendor Admin verifies against submitted evidence and marks passed/failed/pending-correction before activation.
- [ ] Capture supplier tax ID, tax registration type, legal name and supporting tax documents; check completeness/consistency with the supplier master and store the validation result for invoice, withholding tax, SST and MyInvois processing.
- [ ] Capture beneficiary name, bank and account number, requiring verification before supplier activation or any sensitive bank-detail change; place unverified/changed bank information on hold until authorised Finance/Vendor Admin review is complete.
- [ ] Record or receive financial-health information (credit indicators, financial statements, risk rating, review date); flag suppliers with weak/unavailable financial information for enhanced review, mitigation, approval or rejection based on risk level.
- [ ] Calculate/record a supplier risk level using KYC, financial health, category risk, spend, ESG, compliance results and criticality; the risk level determines required due diligence, approval level, review frequency and activation eligibility.
- [ ] Capture the individuals/entities that ultimately own or control the supplier, ownership percentage and supporting documents; route missing or high-risk ownership information to Compliance for clarification, enhanced due diligence or approval.
- [ ] Present the applicable sustainability questionnaire and capture ESG certifications, responses, score, review date and evidence; include the result in supplier risk/sourcing decisions and trigger a new review on expiry.
- [ ] Assign the supplier to the approved procurement category/taxonomy (e.g. UNSPSC where applicable), spend tier and supplier type, controlling which sourcing events the supplier can join.
- [ ] Record expiry dates for licences, insurance, certifications, KYC, bank and other required documents; send reminders before expiry and place the supplier on hold if critical evidence expires without renewal.
- [ ] Allow the supplier to request changes to contact, address, tax, bank and certification information via the portal; highlight sensitive changes, keep old/new values, require approval where needed, and update the supplier master only after validation.
- [ ] Allow authorised users to suspend, deactivate or blacklist a supplier with effective date, reason and approval; prevent a blocked supplier from joining new sourcing events, receiving new POs or being paid while preserving historical transactions.
- [ ] Schedule supplier re-qualification based on risk, category, material change or review frequency; request updated documents and re-run required checks before the supplier can continue to be used or renewed.
- [ ] Provide an onboarding dashboard showing each application's stage, outstanding action, owner, ageing and total onboarding cycle time, so Procurement can identify delayed cases and bottlenecks.
- [ ] Keep a full audit history of supplier submissions, document changes, validation results, approvals, bank changes, activation and deactivation, with user, date, old/new value and reason for each event.
- [ ] For an existing registered supplier selected on a request, display supplier master status and latest due-diligence status (registration active/inactive, 3PC status, ESG score, Associated Person/ABC status, TPRM/materiality) to Procurement without re-running the duplicate check; if any required check is expired/unavailable/invalid, Procurement/the relevant unit determines whether refresh or additional evidence is required before onboarding/award.
- [ ] For a new/non-registered supplier proposed on a request, require a written reason and Procurement Head exception-approval evidence before the request can proceed; run the duplicate-supplier check specifically at this trigger point (name, registration/BRN, email domain, contact email against supplier master) and require confirmation of which record to use if a potential duplicate is found.
- [ ] Determine supplier "materiality" as Material when the engagement involves outsourcing, system access, data access, or business criticality of Business Critical/Regulatory-Safety Critical, or "To be assessed" when supplier is not yet known, otherwise Non-Material; require the Third-Party Due Diligence Form, additional material-supplier documents and a completed TPRM assessment for a Material supplier before due diligence is considered ready.
- [ ] Determine due-diligence applicability by sourcing type (Sourceable / Non-Sourceable) and spend type (Addressable Spend / Non-Addressable Spend): 3-Point Check (3PC) and Supplier ESG Assessment both apply when Sourceable and Addressable; 3PC only applies when Non-Sourceable and Addressable; neither applies when Non-Addressable.
- [ ] Treat a supplier's due diligence as "Ready" only when: 3PC is valid and evidenced (if required), ESG score is at least 50% and evidenced (if required), Associated Person/ABC assessment is Completed and evidenced, and (if the supplier is Material) TPRM is Completed with the TPRM assessment, Third-Party Due Diligence Form and additional material-supplier documents all evidenced.
**Depends on:** M4

### Stakeholders / Governance
- Procurement / Vendor Admin — manage registration, verification and lifecycle.
- Supplier — self-services registration and profile updates via the portal.
- Compliance / Risk — review beneficial ownership, high-risk items and ESG/TPRM outcomes.
- Finance — verifies bank details before activation/changes.

### Process Flow
1. Supplier registers (self-service or Procurement-initiated); the system shows the correct fields/documents for their country/category/risk profile.
2. The system runs duplicate detection, company/tax/bank verification and financial-health/risk scoring, escalating exceptions to Vendor Admin/Compliance as needed.
3. ESG, beneficial ownership and categorisation are captured/assessed; the supplier record remains pending until required checks pass.
4. Once approved, the supplier is active and available to sourcing (M4/M6) and P2P (M10); document expiry, re-qualification, self-service updates and deactivation are managed for the life of the relationship (continues in M12 post-contract).
5. At the point a request proposes a supplier (M1/M4), the system distinguishes existing-registered vs new/non-registered vs not-yet-known suppliers and applies the corresponding duplicate-check, exception-approval and due-diligence-readiness rules above.

### Open TBDs
- Maybank's supplier-onboarding model (bank verification, financial health, risk scoring, beneficial ownership, ESG, categorisation, expiry tracking, self-service, blacklisting, requalification, audit trail) is presented as more granular than the current Etiqa baseline; which of these are already built vs. net-new needs confirmation with the Etiqa process owner before BRD sign-off.

---

## Module: M6 — RFx Design, Event Administration & Submission

### Capabilities

#### Feature: RFx Template Build, Publication & Submission Control
**User Story:** As Procurement (Buyer), I want to build a structured, validated RFx from reusable templates and control its full publication lifecycle, so that every supplier competes on the same, auditable basis.
**Requirements Checklist:**
- [ ] Store reusable technical requirement templates by category (specifications, mandatory questions, response format, attachments); record the exact template version included in each RFx.
- [ ] Store standard commercial response templates (price, currency, quantity, cost breakdown, payment terms, validity period) so all invited suppliers respond using the same structure.
- [ ] Store approved contract terms/conditions, mandatory clauses and supplier response fields; identify clauses open for comment and record every supplier deviation for later legal/contract evaluation.
- [ ] Let the buyer build the RFx using separate technical, commercial, contractual and attachment sections; check section completeness, identify mandatory supplier responses and keep the published version as the controlled evaluation baseline.
- [ ] Let Procurement define evaluation criteria, weightings, minimum scores and assigned evaluators before publication; each evaluator receives only their permitted section, and changes to criteria/evaluators are controlled and recorded.
- [ ] Before publication, check RFx title, requirement, supplier list, dates, templates, scoring model, approvers and attachments for completeness/consistency; block publication until corrected.
- [ ] Invite only approved, eligible suppliers selected for the RFx; record invitation date, contact, delivery status and supplier response; require a reason and controlled update for removed/replaced/additional suppliers.
- [ ] Support both an open tender (any qualified supplier may participate) and an invited tender (only selected approved suppliers may respond); record the chosen approach, eligibility rules and approval before publication.
- [ ] Publish the approved RFx and control its opening date, briefing date, clarification deadline, submission deadline and evaluation milestones; give all suppliers the same published information and automatic reminders.
- [ ] Require a new closing date, reason and approval before extending submission; notify all participating suppliers of the same revised deadline; retain both original and revised deadlines in the audit trail.
- [ ] Timestamp every submission and automatically identify late responses; keep late submissions locked, accepting them only through an authorised exception with a recorded reason, otherwise reject.
- [ ] Allow RFx cancellation/withdrawal only by an authorised user with a documented reason and approval where required; notify suppliers and retain all submitted bids, communications and cancellation evidence.
- [ ] Keep supplier proposals sealed/unavailable to evaluators until the submission deadline and an authorised opening event; record who opened it, the time, submissions received and any missing/late response.
**Depends on:** M4, M5

### Stakeholders / Governance
- Procurement (Buyer / Category Manager) — builds, publishes and administers the RFx.
- BU / Legal — participate in evaluator assignment and contract-clause definition.
- Supplier — receives invitation, submits response.

### Process Flow
1. Buyer selects/updates technical, commercial and contract templates and builds the RFx sections.
2. Buyer defines evaluation criteria/weightings and assigns evaluators; the system validates RFx configuration before publish.
3. Buyer selects the eligible supplier list (open or invited tender) and publishes with controlled dates.
4. Suppliers submit sealed responses before the deadline; extensions, late submissions and cancellations are all controlled, reasoned and audited.
5. After closing, an authorised user opens the sealed proposals, which then proceed to Evaluation (M7).

### Open TBDs
- None stated beyond the general "validate ownership/thresholds with the process owner before BRD sign-off" caveat that applies across modules.

---

## Module: M7 — Technical, Commercial & Contract Evaluation

### Capabilities

#### Feature: Structured Evaluation, Clarification & Status Gate
**User Story:** As an Evaluator / Procurement / Legal reviewer, I want a controlled workspace to score technical, commercial and contract responses and manage clarifications, so that only compliant, well-substantiated supplier responses proceed to award.
**Requirements Checklist:**
- [ ] Present approved technical requirements and scoring criteria beside each supplier response; let evaluators enter scores/comments, attach evidence and declare conflicts; lock the result after submission unless an authorised reopening is recorded.
- [ ] Compare supplier pricing, currency, cost breakdown, payment terms and commercial conditions using the same approved evaluation structure; record scores/adjustments/comments and protect restricted commercial information from unauthorised evaluators.
- [ ] Show supplier comments/deviations against each contract clause; let Legal and Procurement record risk, acceptance, required amendment and owner; require unresolved material deviations to be closed or approved before award.
- [ ] Provide one controlled workspace for evaluators to raise clarification questions rather than contacting suppliers separately; record question, owner, due date, supplier reply, attachments and closed/open status.
- [ ] Maintain a list of clarification issues and technical/commercial/contractual deviations per supplier, each with status, owner, due date, impact and resolution; unresolved material items block progression.
- [ ] Distribute common clarifications, amendments and revised deadlines consistently to all affected suppliers; record who received the information and prevent private/unrecorded communication from changing the evaluation basis.
- [ ] Submit a material change to scope, criteria, timeline or contract terms for impact review and approval before release; keep old/new versions, approval reason and supplier communication; may require suppliers to resubmit a revised response.
- [ ] Apply the approved minimum score, mandatory requirement and unresolved-deviation rules to decide whether a supplier proceeds to negotiation/award or is stopped with a recorded reason.
- [ ] Flag unusual scoring patterns (large differences between evaluators, missing comments, identical scores, out-of-range scores); let Procurement review the alert and record whether correction, clarification or no action is required.
**Depends on:** M6

### Stakeholders / Governance
- BU / Procurement / Legal — evaluate technical, commercial and contract dimensions respectively.
- Evaluator — scores and raises/tracks clarifications.
- System — flags scoring anomalies.

### Process Flow
1. Evaluators score technical, commercial and contract responses in the shared workspace, using the locked published RFx baseline.
2. Clarifications and deviations are raised, tracked and resolved (or escalated) through the controlled workspace.
3. Material changes go through impact review/approval before release to suppliers.
4. The status gate determines which suppliers proceed to negotiation (M8) or are stopped.

### Open TBDs
- None additional stated beyond the general BRD sign-off caveat.

---

## Module: M8 — Negotiation, eAuction, BAFO & Consolidation

### Capabilities

#### Feature: Negotiation Rounds, Award Scenario Comparison & Savings Calculation
**User Story:** As Procurement (Category Manager), I want to run controlled negotiation/eAuction rounds and consolidate the results into a comparable award recommendation with a validated savings figure, so that the final award decision is defensible and its value is measurable.
**Requirements Checklist:**
- [ ] Record each negotiation round, meeting, supplier offer, revised price, commercial term and agreed action; for eAuction, control event rules/timing and keep bid history without unauthorised changes.
- [ ] Allow Procurement to request a revised offer or Best and Final Offer (BAFO) with a clear deadline and response format; store each submission as a separate round so original, revised and final offers can be compared.
- [ ] Record negotiated changes to payment terms, liability, service levels, pricing and other contract conditions, linked to the relevant contract clause, agreed before contract finalisation.
- [ ] Combine the approved technical, commercial and contract evaluation results, clarifications, deviations and negotiation outcomes into one supplier summary; calculate the total result using the approved weighting and retain underlying evidence.
- [ ] Let Procurement compare single award, split award, different supplier combinations and other approved scenarios using price, score, risk, capacity and business impact; include the selected scenario and reason in the award recommendation.
- [ ] Calculate savings or cost avoidance by comparing the approved baseline with the negotiated/awarded value using an agreed method, distinguishing hard savings (actual spend reduction) from soft savings/cost avoidance (future increase prevented); record calculation, baseline source, currency, owner and Finance validation status.
- [ ] Compare the proposed price with previous purchases, contract prices, supplier offers, market benchmarks or expected cost; highlight significant variances and require explanation before award approval.
**Depends on:** M7

### Stakeholders / Governance
- Procurement (Category Manager) — runs negotiation/eAuction, compares scenarios.
- Legal — negotiates contract commercial terms.
- Finance — validates the calculated savings.

### Process Flow
1. Procurement negotiates price/commercial terms directly or via eAuction, in one or more recorded rounds; BAFO may be requested.
2. Contract commercial terms are negotiated in parallel/afterward with Legal involvement.
3. All evaluation, clarification, deviation and negotiation outcomes are consolidated into a single comparable supplier summary.
4. Procurement compares award scenarios and calculates savings/price variance to support the award recommendation (M9).

### Open TBDs
- None additional stated beyond the general BRD sign-off caveat.

---

## Module: M9 — Award, Due Diligence & Contract Execution

### Capabilities

#### Feature: Award Recommendation & Final Due Diligence
**User Story:** As Procurement (Buyer), I want the system to compile an award pack and re-check supplier due diligence before award, so that only a fully cleared supplier is awarded.
**Requirements Checklist:**
- [ ] Prepare an award pack containing evaluation result, negotiation outcome, supplier risk, commercial comparison, savings and recommended supplier; route the recommendation per active approval limits and record the final decision.
- [ ] Before award, check that the selected supplier has completed all due diligence required by category, value, country and risk; stop the award on failed/expired checks unless an authorised exemption, mitigation and approval are recorded.
- [ ] Review the latest financial-health result, risk indicator and review date for the selected supplier; require mitigation, additional approval or rejection on high/deteriorating risk before contract execution.
- [ ] Review the supplier ESG score, high-risk answers, certifications and required corrective action; escalate material ESG risk, requiring it to be accepted, mitigated or resolved before award.
- [ ] Record required compliance checks, result, supporting documents and any exemption/waiver; a failed check blocks award, and an exemption must show reason, mitigating control, approver and validity period.
**Depends on:** M8

#### Feature: Contract Lifecycle Management, Repository & Catalogue
**User Story:** As Procurement / Legal, I want to draft, sign, store and version every contract in one controlled repository and flip its terms into a purchasing catalogue, so that downstream PR/PO activity only ever uses approved, current commercial terms.
**Requirements Checklist:**
- [ ] Manage the contract from drafting/negotiation through approval, signature, effective date, amendments, renewal and expiry; track status, owner, obligations and key dates; prevent use of an unapproved or expired contract.
- [ ] Store the signed contract, approved versions, redlines, approvals and supporting documents in one controlled, role-based-access repository; identify the current authoritative version without overwriting history.
- [ ] Support approved templates, clause selection, redlining, comments and version comparison during drafting; show author/date/change per version; only the approved final version proceeds to signature.
- [ ] Route the final contract to authorised signatories based on entity, value and approval limits; record digital signatures and execution dates; block signing by anyone without valid authority.
- [ ] Convert approved contract items, prices, discounts, units, validity dates and order limits into a controlled catalogue/pricebook; restrict PR/PO users to selecting only active approved terms; require an amendment or authorised update to change them.
- [ ] After execution, create the official AGMT ID, linking it to the supplier, signed contract, approval, effective dates and pricebook; pass the AGMT ID to PR, PO, invoice and reporting records for end-to-end traceability.
**Depends on:** M9 (Award Recommendation & Final Due Diligence feature, same module)

### Stakeholders / Governance
- Procurement (Buyer / Category Manager) — prepares award pack, manages contract catalogue.
- Vendor Admin / Compliance / Risk — perform/confirm final due diligence, financial and ESG risk checks.
- Legal — drafts, redlines and reviews contract terms.
- Authorised Signatory — executes the contract within their delegated limit.

### Process Flow
1. Procurement compiles the award recommendation from the consolidated M8 outcome and routes it for approval.
2. Final supplier due diligence (financial, ESG, compliance) is re-checked immediately before award; exceptions require a recorded exemption.
3. Once awarded, the contract is drafted, versioned, redlined and routed to an authorised signatory for digital signature within their approval limit.
4. The executed contract is stored in the central repository, its terms are flipped into the pricebook/catalogue, and an AGMT ID is issued and carried into downstream PR/PO/invoice records (M10).

### Open TBDs
- None additional stated beyond the general BRD sign-off caveat.

---

## Module: M10 — PR/PO, Delivery & Supplier e-Invoice 3-Way Match

### Capabilities

#### Feature: PR/PO Creation, Delivery Acceptance & Invoice 3-Way Match
**User Story:** As Requester / Procurement / Finance (AP), I want the PR/PO created from an approved award/contract, delivery evidence recorded, and every invoice matched against the PO and receipt before payment is prepared, so that Etiqa only pays for what was approved and received.
**Requirements Checklist:**
- [ ] Create/pre-populate the PR from the approved request, award, supplier, contract or AGMT ID and budget details; requester confirms item, quantity, delivery, cost centre and GL account before submitting.
- [ ] Select catalogue when an active contracted item is available, non-catalogue when the approved item is not in the catalogue, or manual PO only under an authorised exception; record the selected route and any manual-PO justification.
- [ ] Recheck budget, contract, supplier and approval authority, then route the PR to the correct approver; after approval, create the PO, assign a PO number and send the controlled PO to the supplier; return rejected PRs to the requester with a reason.
- [ ] Before PO creation, confirm the contract/AGMT ID is active, item and supplier match, and price/validity dates are current; block expired contracts, inactive suppliers or price differences pending correction or approval.
- [ ] Record quantity received/milestone completed, date and acceptance result against the PO (delivery/GR/service acceptance); record rejected or partially accepted delivery and return it for correction before invoice matching.
- [ ] Determine whether a transaction uses a normal supplier e-Invoice, another approved invoice route, or a later self-billed process, based on supplier status, transaction type and tax rules; the selected treatment controls required invoice/MyInvois evidence.
- [ ] Capture supplier invoice number, date, amount, tax, PO and GR/service-acceptance reference together with the MyInvois unique reference, QR and validation status where required; place invalid/missing MyInvois evidence on hold.
- [ ] Auto-create the ePV payment voucher using the approved PO, GR/service acceptance, invoice, supplier, tax, cost centre and GL information; link all supporting documents; hold pending until duplicate and matching checks complete.
- [ ] Compare supplier, invoice number, date, amount, currency and PO against existing/historical invoices; block possible duplicates and route to AP for review; only a confirmed non-duplicate continues.
- [ ] Compare PO quantity/price, GR/service acceptance, and supplier invoice amount within the approved tolerance (3-way match); proceed to approval on success, or create an exception showing the exact quantity/price/amount difference on mismatch.
- [ ] Assign each invoice mismatch to Finance, the supplier or the business owner with reason and due date; resolve through correction, dispute, credit note, debit note or refund note, and re-match before payment.
- [ ] After a successful match, route the ePV to the authorised approver and perform required control checks; mark approved vouchers ready for PV/EPRF/BOT processing; return rejected vouchers to the preparer with a reason.
- [ ] Provide a Workspace Requisitions list/table (Ref, Title, Category, Role, HC, Est. Cost, Reserved, Status, Action) filterable by status (Draft, Submitted, Validated (pending approval), Budget Hold, Approved, Hiring, Onboarding, Fulfilled, Rejected, Cancelled) and category (Change the Business, Run the Business), matching the existing Finance Portal Workspace — Requisitions screen (the reference screen shows a Headcount Hiring Request variant of a requisition).
- [ ] Provide a Workspace Approval Inbox (Request ID, Request Type, Submitter, Amount, Cost Centre, Submitted On, Status, Action) filterable by status (All, Draft, Pending Approval, Approved, Rejected, Cancelled, Executed, Returned for Rework) and request type, with View Details / Approve actions per request, matching the existing Finance Portal Workspace — Approval Inbox screen.
- [ ] Provide a Workspace Dashboard summarising budget health (allocated/available/reserved/spent, CAPEX/OPEX split), a spend-trend chart by month, a "Pending My Approval" queue and a Finance Console requisition-finalisation queue, matching the existing Finance Portal Workspace — Dashboard screen.
**Depends on:** M9 (for sourced/contracted spend); can start directly from an approved M3 request for non-sourced, catalogue or below-threshold spend not requiring M4–M9.

### Stakeholders / Governance
- Requester / Procurement — create and route the PR/PO.
- Supplier / Business / Technical Owner — provide and record delivery/GR/service acceptance.
- Finance / AP / ePV — determine invoice treatment, run duplicate check and 3-way match, approve the voucher.

### Process Flow
1. PR is created (or pre-populated) from the approved request/award/contract and routed to catalogue, non-catalogue or manual-PO handling.
2. PR is approved and the PO is issued to the supplier, after a fresh budget/contract/price validity check.
3. Delivery/GR/service acceptance is recorded against the PO.
4. Invoice treatment is decided and the supplier e-Invoice/MyInvois reference is captured; the ePV voucher is auto-created.
5. Duplicate-invoice check and 3-way match run; mismatches are resolved through the exception queue; a successful match proceeds to ePV approval, which hands off to Payment (M11).

### Open TBDs
- Exact ownership, thresholds, exception ageing and system-of-record boundaries for this module must be validated with the relevant Etiqa process owner before BRD sign-off.

---

## Module: M11 — Payment, Self-Billed e-Invoice, GL Posting & Bank Reconciliation

### Capabilities

#### Feature: Payment Instruction, Approval & Execution
**User Story:** As Finance, I want to prepare, approve and execute the correct payment instruction with all beneficiary and duplicate/limit controls applied, so that payments are only released to the right party, once, within approved limits.
**Requirements Checklist:**
- [ ] Select PO payment when supported by an approved PO and matched invoice, direct payment for an approved non-PO exception, or TT (bank transfer) for an authorised transfer route; record the chosen route, reason and supporting approval.
- [ ] Prepare the applicable PV (Payment Voucher), EPRF (Expenditure and Payment Requisition Form) or BOT instruction using the approved voucher, payee, bank, amount, currency, payment date and purpose; assign a unique reference; block progress without required supporting documents.
- [ ] Apply the correct tax code, SST, withholding tax and other tax treatment using supplier and transaction information; calculate payable/tax amounts, retain the LHDN reference where relevant, and send unclear cases to Finance/Tax for review.
- [ ] Route the payment instruction to the authorised approver based on value, entity and payment type, and check segregation of duties; return rejected payments to the preparer with reason and audit history.
- [ ] Verify beneficiary and bank account, check for duplicate payment, confirm the amount is within the approved limit and validate required bank details before release; block and raise an exception on any failed check.
- [ ] Send the approved payment instruction to the bank; record bank reference, execution date and success/rejection/pending status; return failed payments to an exception queue for investigation, correction and controlled reprocessing.
**Depends on:** M10

#### Feature: Self-Billed MyInvois, GL Posting & Bank Reconciliation
**User Story:** As Finance / Tax, I want the system to determine self-billed e-Invoice applicability, post the correct GL entries and reconcile bank settlement, so that every payment is correctly taxed, accounted for and closed with full audit evidence.
**Requirements Checklist:**
- [ ] After successful payment, check supplier type, payment category and LHDN rules to decide whether Etiqa must issue a self-billed e-Invoice; only applicable transactions continue to self-billed generation (self-billing is not required for every payout, only LHDN-defined categories).
- [ ] For an applicable transaction, create the buyer-issued self-billed payload using supplier, payment, tax and transaction details, and submit it to MyInvois; retain submission date, payload version and transaction reference.
- [ ] Receive and store the MyInvois unique ID, QR, validation date and accepted/rejected status; link an accepted response to the payment, and route a rejected response to the MyInvois exception queue with the rejection reason.
- [ ] Allow Finance/Tax to review a MyInvois rejection, correct the relevant supplier/tax/transaction data and resubmit; keep every rejected and corrected version and support cancellation/adjustment when resubmission is not appropriate.
- [ ] Link the voucher, payment, supplier invoice or self-billed ID, GL posting and bank reference using common transaction IDs, so users/auditors can trace the transaction end-to-end without manually matching separate records.
- [ ] Post approved AP, expense or asset, tax and withholding-tax entries to the correct entity, cost centre and GL account; update budget actuals or release the commitment, and record any reversal/adjustment.
- [ ] Receive the bank statement/payment advice and capture bank reference, date, amount, currency and status, making the record available for matching.
- [ ] Match the bank record to the payment, supplier, invoice or self-billed reference and amount; a successful match clears the AP item, while unmatched/partial items move to the reconciliation exception queue.
- [ ] Assign failed payments and unmatched bank items to an owner with reason and ageing; investigate/adjust/reprocess the transaction and record the final resolution before closure.
- [ ] Assemble and retain the request, approvals, supplier, contract, PO, GR, invoice, ePV, payment, MyInvois, GL and bank evidence in CCM per the retention rule; provide a dashboard of completion and outstanding exceptions before the transaction is closed.
- [ ] Provide a Finance Payment Vouchers / Vendors / Fin Ops Config / Reports / System Settings navigation and a Finance System dashboard (budget health, spend trend, pending-my-approval queue, requisition-finalisation queue), matching the existing Finance Portal Finance System home screen.
**Depends on:** M11 (Payment Instruction, Approval & Execution feature, same module)

### Stakeholders / Governance
- Finance / Approver — approves the payment instruction and reviews SoD.
- Finance / Payout / Bank — execute and confirm payment.
- Finance / Tax — apply tax treatment and manage self-billed MyInvois submission/exceptions.
- Finance / GL, Finance / Bank Recon — post GL entries and reconcile bank settlement.
- Finance / Procurement / Audit / CCM — assemble and retain the audit pack.

### Process Flow
1. Finance selects the payment route and prepares the PV/EPRF/BOT instruction with tax treatment applied.
2. The instruction is approved (with SoD check) and passes beneficiary/duplicate/limit controls before Payout executes it against the bank.
3. Self-billed applicability is checked; applicable transactions are submitted to MyInvois, with rejections corrected and resubmitted via the exception queue.
4. GL entries are posted; the bank statement is received and reconciled against the payment; unmatched items go to an exception queue.
5. The complete transaction evidence is assembled and retained in CCM, closing the transaction and feeding Post-Contract reporting (M12).

### Open TBDs
- Exact ownership, thresholds, exception ageing and system-of-record boundaries for this module must be validated with the relevant Etiqa process owner before BRD sign-off.

---

## Module: M12 — Post-Contract Supplier Management, Savings & Audit

### Capabilities

#### Feature: Contract Governance, Supplier Performance & Savings Validation
**User Story:** As Procurement / Vendor Management / Finance, I want ongoing visibility of contract obligations, supplier performance, market conditions and validated savings after award, so that renewal, dispute and sourcing decisions are evidence-based.
**Requirements Checklist:**
- [ ] Record contract obligations, deliverables, owners, due dates, renewal dates, amendments and expiry; send reminders and escalate overdue obligations so the contract is managed before breach or missed renewal.
- [ ] Bring together supplier, category, price, spend and available market information after award, so Procurement can identify price movements, new supplier options, supply risk and opportunities for the next sourcing cycle.
- [ ] Record contract variations and disputes with the affected clause, PO or GR, supplier, financial impact, owner, status and evidence; require approval for changes and escalate unresolved disputes until closure.
- [ ] Record delivery quality, service incidents, technical results, corrective actions and stakeholder feedback for the supplier; assign performance issues to an owner, potentially triggering improvement action, escalation or future sourcing restrictions.
- [ ] Compare actual KPI and SLA results with the contracted target each review period; highlight breaches, record service credits/corrective actions, and reflect repeated underperformance in supplier review/renewal decisions.
- [ ] Compare contracted quantity/capacity with actual usage, spend and forecast demand; use the information to adjust commitments, consolidate future demand, avoid unused services and plan the next sourcing event.
- [ ] Segment the supplier by category, spend, risk, criticality and performance and record relationship actions, reviews and improvement plans; treatment can differ for strategic, preferred, transactional or high-risk suppliers.
- [ ] Compare the approved baseline, negotiated award, contract price, PO value and actual spend to calculate savings/cost avoidance; require Finance review of the method/evidence, with only validated savings included in official reporting.
- [ ] Start supplier re-qualification at the scheduled date, before renewal, or after a material change; review updated KYC, bank, tax, financial, ESG and certification evidence; failed checks can place the supplier on hold or block renewal.
- [ ] Create an end-to-end P2P audit pack and dashboard using linked request, supplier, sourcing, contract, PO, GR, invoice, payment, GL and bank records; show approvals, exceptions, cycle time, spend, savings and outstanding actions, with CCM retaining supporting evidence; retain procurement records for at least 15 years subject to the approved records schedule, and record User ID, action, timestamp, previous value, new value and approval history for each transaction.
**Depends on:** M9, M11

### Stakeholders / Governance
- Procurement / Vendor Management — track obligations, manage disputes, run SRM segmentation.
- Requester / BU / Stakeholder — feed performance and KPI/SLA observations.
- Finance — validates calculated savings.
- Audit / CCM — retain the P2P audit evidence.

### Process Flow
1. After contract execution, obligations, renewal dates and amendments are tracked with reminders/escalation.
2. Supplier market conditions, performance, KPI/SLA results and demand/consumption are monitored on an ongoing basis.
3. Variations/disputes are recorded and resolved with approval where required.
4. Savings are calculated against the approved baseline and validated by Finance; supplier re-qualification runs on schedule, before renewal, or on material change.
5. A consolidated end-to-end P2P audit pack/dashboard is available at any time, retained per the approved records schedule (at least 15 years).

### Open TBDs
- Maybank's post-contract model (contract obligations, market intelligence, commercial disputes, operational/technical performance, contract KPIs, demand/consumption, SRM segmentation) is presented as more explicit than the current Etiqa baseline; confirmation of which capabilities are already built vs. net-new is required from the Etiqa process owner before BRD sign-off.

---

## Open TBDs (cross-module)

- **Ownership, thresholds, exception-ageing and system-of-record validation**: repeated verbatim across nearly every module in the source — "process ownership, approval thresholds, system-of-record boundaries and implementation priority must be validated with the relevant Etiqa process owner before BRD sign-off." This applies to every approval gate, exception route and threshold value captured in this KB (including the specific RM/currency thresholds and approval-level bandings taken from the prototype, which are stated there as "illustrative").
- **Maybank-benchmark capabilities not confirmed for Etiqa**: the source explicitly distinguishes "Existing ETQ" (already evidenced), "Existing ETQ - Enhance," "Recommendation" (proposed based on the Maybank benchmark, not yet an Etiqa requirement) and "Optional Future" (not a Day-1 requirement) for individual capabilities, but does not tag every single system feature in the P1–P12 tables with one of these four statuses. Which specific features in this KB are current-state-existing vs. net-new-recommended needs to be confirmed feature-by-feature with the Etiqa process owner; the source's own Priority Roadmap (P0/P1/P2 phases) is the closest available prioritisation signal and is carried into the Technical KB's Open Decisions.
- **"Miss post contract management"** — a short, unexplained annotation appears in the source directly before the P1 section (with an adjacent stray numeric artifact `-1885956667500`, apparently a document-formatting/paste artifact rather than content). Its meaning is unclear from context; flagging rather than interpreting it.
- **Role model reconciliation**: the eProcurement prototype exposes exactly three login roles (IT Business Requestor, Non-IT Business Requestor, Procurement Administrator) with fixed category/step sets, while the S2P Flow document names many additional process owners (Finance, CFO, SMC, Board, Vendor Admin, Compliance, Risk, Legal, Category Manager, Bank Ops, Payout, Audit, CCM, etc.) without defining their system login/role model. How the prototype's three-role UI model maps onto (or is superseded by) the full owner list in the S2P document is not stated and needs confirmation.
- **Finance Portal vs. eProcurement Prototype as system-of-record for M1/M2/M10/M11 screens**: the existing Finance Portal (Workspace Dashboard/Requisitions/Approval Inbox, Finance System/Budget/Requisition Finalisation) already implements parts of M2 and M10 with its own data shown (e.g. sample Requisition REQ-2026-001 through REQ-2026-004, sample cost centre TO110, sample fiscal-year FY2026 figures). Whether the target system extends this existing Finance Portal or is a separate new build that must integrate with it is not stated and needs confirmation.
