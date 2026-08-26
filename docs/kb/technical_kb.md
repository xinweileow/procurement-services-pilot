# Technical Knowledge Base (FSD) — Etiqa Procurement System

## Conventions

- **Base path / versioning:** `/api/v1/...` for every endpoint in every module.
- **Field naming / casing:** camelCase for every JSON field, request and response; primary keys are `id`; foreign keys are `<entity>Id` (e.g. `requestId`, `supplierId`).
- **Timestamps:** ISO 8601 UTC (`2026-08-24T09:15:00Z`) for every date/time field; date-only fields (e.g. delivery date) use `YYYY-MM-DD`.
- **Error response shape:** two developer templates are committed under `.loop-eng/agent-templates/` with two different stated shapes — `developer-dotnet.md` mandates RFC 7807 `ProblemDetails`/`ValidationProblemDetails` (`application/problem+json`) for every non-2xx response, while `developer-backend.md` (Python/FastAPI) only says "structured error responses with a consistent shape (e.g. `{"detail": "..."}`)" without committing to full ProblemDetails. `developer-frontend.md` explicitly states the API client parses RFC 7807 `ProblemDetails` via "one shared parser," which only works if the backend that frontend actually calls uses that shape. **Decision for this FSD:** all backend modules in this system use the **dotnet** stack (`stack: dotnet`) and RFC 7807 `ProblemDetails` as the single error-response shape, so every module's error shape matches what `developer-frontend.md` already assumes; the Python `developer-backend.md` template is not used by this project's tickets. This choice (dotnet over Python backend) is recorded under Open Decisions below for human confirmation rather than left as a silent assumption.
- **Auth:** every endpoint requires a bearer token (`Authorization: Bearer <token>`) except none — auth scheme/provider is not stated by any source (see Open Decisions).
- **Pagination:** `?page=1&pageSize=20`, response wrapped as `{ items: [...], page, pageSize, total }`, matching the Finance Portal's existing page-size (20/50/100) and "Showing X to Y of Z entries" pattern.

---

## Module: M1 — Demand Intake & Business Case

### Feature Trace

#### Feature: Guided Demand Intake & Business Case Capture
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| Select role on Login / Role Selector | `onRoleChange` | `GET /api/v1/me/role-config?role={role}` | Procurement Service | RoleAccessConfig (static/config, not persisted) |
| Search catalogue on Catalogue Search screen | `onCatalogueSearch` | `GET /api/v1/catalogue/items?query={q}` | Procurement Service | CatalogueItem |
| Click "Item not available - proceed as Non-Catalogue" | `onMarkNonCatalogue` | n/a (client-side state only, per prototype) | — | Request.isNonCatalogue (set on submit) |
| Fill Request Type / Request Details landing form, click "Continue" | `onSaveDraftOrContinue` | `POST /api/v1/requests` (first save) then `PATCH /api/v1/requests/{id}` | Procurement Service | Request |
| Upload evidence in an `EvidenceUpload` zone | `onFilesAdded` | `POST /api/v1/requests/{id}/attachments` | Procurement Service | RequestAttachment |
| Click "Save draft" | `onSaveDraft` | `PATCH /api/v1/requests/{id}` (status=draft) | Procurement Service | Request |
| Click "Continue" on final Review & declare step / "Submit request" | `onSubmitRequest` | `POST /api/v1/requests/{id}/submit` | Procurement Service | Request, RoutingDecision |
| View "Pending My Approval" / Finance Console preview (Workspace Dashboard) | `onLoadDashboard` | `GET /api/v1/dashboard/summary` | Procurement Service + Budgeting (aggregated) | Request, ApprovalTask, Requisition |
| Open "Requisitions" list (Workspace — Requisitions) | `onLoadRequisitions` | `GET /api/v1/requests?status={status}&category={category}` | Procurement Service | Request |
| Procurement Administrator: review request on Procurement Triage Overview, click Accept/Return/Assign | `onTriageAction` | `POST /api/v1/requests/{id}/triage-decision` | Procurement Service | Request, TriageDecision |
| System runs duplicate/anti-splitting check on submit | n/a (server-side, no direct UI action) | `POST /api/v1/requests/{id}/submit` (server-side side effect) | Procurement Service | Request, DuplicateFlag |

**Ticketing Hints:** stack: dotnet (backend, this Feature's own trace assumes `developer-dotnet.md`'s conventions) + stack: frontend (the intake wizard UI is a separate ticket, linked via `depends_on`, since this Feature's rows span both a new UI (`likely scope: src/features/requestor-intake/`) and new endpoints (`likely scope: src/Procurement.Api/Controllers/RequestsController.cs`, `src/Procurement.Api/Controllers/CatalogueController.cs`)) | likely scope: `src/features/requestor-intake/`, `src/Procurement.Api/Controllers/RequestsController.cs`, `src/Procurement.Api/Controllers/CatalogueController.cs`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/requests` returns 422 when title, description, category, department or deliveryDate is missing.
- [ ] `POST /api/v1/requests/{id}/submit` returns 409 when the request's `status` is already `submitted` or beyond (idempotency/duplicate-submit guard).
- [ ] `POST /api/v1/requests/{id}/submit` returns 400 when the four compliance declarations are not all `true`.
- [ ] `POST /api/v1/requests/{id}/attachments` returns 413-equivalent 422 when a file exceeds 20 MB.
- [ ] `GET /api/v1/requests/{id}` returns 404 when the request does not exist.
- [ ] `POST /api/v1/requests/{id}/submit` returns 200 with a `routingDestination` of `"GSP"` when `estimatedValue >= 100000` and `country == "Malaysia"`, otherwise `"EtiqaInternalProcurement"`.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| GET | /api/v1/catalogue/items | bearer token | query: `query` | `{items: CatalogueItem[]}` | 401 unauthenticated |
| POST | /api/v1/requests | bearer token | `{title, description, category, department, country, entity, deliveryDate, criticality, engagementPathway, procurementNature, previousId?}` | `{id, status: "draft", ...}` | 401 unauthenticated, 422 missing required field |
| PATCH | /api/v1/requests/{id} | bearer token | partial `Request` fields | `{id, ...updated fields}` | 400 malformed, 401 unauthenticated, 404 not found, 409 request already submitted |
| POST | /api/v1/requests/{id}/attachments | bearer token | multipart file + `documentType` | `{id, fileName, size, documentType}` | 401 unauthenticated, 404 request not found, 422 file too large / unsupported type |
| POST | /api/v1/requests/{id}/submit | bearer token | `{declarations: {noConflict, connectedPartyDeclared, noSplittingDeclaration, completeDeclaration}}` | `{id, status: "submitted", requestId, routingDestination}` | 400 declarations incomplete, 401 unauthenticated, 404 not found, 409 already submitted, 422 blocking validation issue present |
| GET | /api/v1/requests | bearer token | query: `status, category, page, pageSize` | `{items: Request[], page, pageSize, total}` | 401 unauthenticated |
| GET | /api/v1/requests/{id} | bearer token | — | `Request` | 401 unauthenticated, 404 not found |
| POST | /api/v1/requests/{id}/triage-decision | bearer token (Procurement Administrator) | `{decision: "accept"\|"return"\|"reject"\|"assign", reason?, assigneeTeam?}` | `{id, status, triageDecision}` | 400 missing reason for return/reject, 401 unauthenticated, 403 not a Procurement Administrator, 404 not found |
| GET | /api/v1/dashboard/summary | bearer token | — | `{budgetHealth, spendTrend, pendingApprovals: [...], financeConsole: [...]}` | 401 unauthenticated |

### Data Model

#### Entity: Request
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required, unique, system-generated display ID (e.g. `PR-2026-000123`) | — |
| requesterId | string | required | references User.id |
| businessUnit | string | required | — |
| costCentre | string | required | — |
| category | string | required, one of the role's allowed category set | — |
| estimatedValue | decimal | required, > 0 | — |
| currency | string | required, one of `MYR, USD, SGD` | — |
| engagementPathway | string | required, one of `Sourcing with Contract, Sourcing Only, Contract Only, Other Query` | — |
| procurementNature | string | required, one of `New Procurement, Renewal, Variation Order, Contract Extension` | — |
| previousContractId | string | required when procurementNature is Renewal/Variation Order/Contract Extension | references Contract.id |
| country | string | required | — |
| entity | string | required | — |
| deliveryDate | date | required | — |
| criticality | string | one of `Standard, Important, Business Critical, Regulatory / Safety Critical` | — |
| isNonCatalogue | boolean | required | — |
| status | string | required, one of `draft, submitted, validated, budget_hold, approved, rejected, cancelled` | — |
| routingDestination | string | set on submit, one of `EtiqaInternalProcurement, GSP` | — |
| submittedAt | datetime (UTC) | set on submit | — |

#### Entity: RequestAttachment
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| documentType | string | required (e.g. `businessCase, scope, otherRequest, existingContract`) | — |
| fileName | string | required | — |
| sizeBytes | integer | required, <= 20971520 | — |
| uploadedAt | datetime (UTC) | required | — |

#### Entity: TriageDecision
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| decidedBy | string | required | references User.id |
| decision | string | required, one of `accept, return, reject, assign` | — |
| assigneeTeam | string | required when decision=assign, one of `GSP, EtiqaInternalProcurement, RiskTPRM, LegalContract, SupplierRegistration` | — |
| reason | string | required when decision is return/reject | — |
| decidedAt | datetime (UTC) | required | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | Catalogue search, request intake and triage are internal to Procurement Service; no external system call is described in either source. |

### Open Decisions (TBD)
- Whether the login/role model is exactly the prototype's three roles (IT Business Requestor, Non-IT Business Requestor, Procurement Administrator) or maps onto a broader identity/role system is unconfirmed (carried from business_kb.md's cross-module Open TBD).
- Whether this system extends the existing Finance Portal's actual Request/Requisition data or is a new system integrating with it is unconfirmed.

---

## Module: M2 — Budgeting & Budget Control

### Feature Trace

#### Feature: Budget Availability, Commitment & Exception Control
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| Change Fiscal Year / Month / Cost Type filter on Finance — Budget screen | `onBudgetFilterChange` | `GET /api/v1/budgets?fiscalYear={fy}&month={m}&costType={ct}` | Budgeting Service | Budget, BudgetActual |
| n/a (backend-only) — budget availability check on request/PR submission | `checkBudgetAvailability` | `POST /api/v1/budgets/availability-check` | Budgeting Service | Budget, BudgetCommitment |
| n/a (backend-only) — budget exception routing when insufficient | `createBudgetException` | `POST /api/v1/budget-exceptions` | Budgeting Service | BudgetException |
| Click "Release" / "Finalise" on Requisition Finalisation screen | `onReleaseOrFinalise` | `POST /api/v1/requisitions/{id}/release` or `POST /api/v1/requisitions/{id}/finalise` | Budgeting Service | Requisition, BudgetCommitment |
| Search / paginate on "All Finalised Requisitions" table | `onSearchFinalised` | `GET /api/v1/requisitions?status=finalised&search={q}&page={p}&pageSize={ps}` | Budgeting Service | Requisition |

**Ticketing Hints:** stack: dotnet | likely scope: `src/Procurement.Api/Controllers/BudgetsController.cs`, `src/Procurement.Api/Controllers/RequisitionsController.cs` (backend) — paired with stack: frontend for the Budget Console / Requisition Finalisation screens, linked via `depends_on` | likely scope: `src/features/budget-console/`, `src/features/requisition-finalisation/`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/budgets/availability-check` returns `{sufficient: false, shortfall}` (200, not an error) when the requested amount exceeds the available balance for the given cost centre/GL account/fiscal year, and the caller is expected to create a `BudgetException` next.
- [ ] `POST /api/v1/budget-exceptions` returns 422 when `shortfall <= 0` (nothing to except).
- [ ] `POST /api/v1/requisitions/{id}/finalise` returns 409 when the requisition's status is not `awaiting_finalisation`.
- [ ] `GET /api/v1/budgets` returns 400 when `fiscalYear` is not a valid 4-digit year.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| GET | /api/v1/budgets | bearer token | query: `fiscalYear, month, costType, costCentre` | `{allocated, reserved, actualSpend, available, byCostCentre: [...], byMonth: [...]}` | 400 invalid fiscalYear, 401 unauthenticated |
| POST | /api/v1/budgets/availability-check | bearer token | `{costCentre, glAccount, fiscalYear, requestedAmount, currency}` | `{sufficient, availableBalance, shortfall}` | 401 unauthenticated, 422 missing field |
| POST | /api/v1/budget-exceptions | bearer token | `{requestId, costCentre, glAccount, shortfall, reason}` | `{id, status: "pending_approval"}` | 401 unauthenticated, 422 shortfall<=0 or missing reason |
| GET | /api/v1/requisitions | bearer token | query: `status, search, page, pageSize` | `{items: Requisition[], page, pageSize, total}` | 401 unauthenticated |
| POST | /api/v1/requisitions/{id}/release | bearer token | — | `{id, status: "released"}` | 401 unauthenticated, 404 not found, 409 wrong status |
| POST | /api/v1/requisitions/{id}/finalise | bearer token | `{finalSpent, notes?}` | `{id, status: "finalised", finalSpent, finalisedOn}` | 401 unauthenticated, 404 not found, 409 wrong status, 422 missing finalSpent |

### Data Model

#### Entity: Budget
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| fiscalYear | integer | required | — |
| entity | string | required | — |
| businessUnit | string | required | — |
| costCentre | string | required | — |
| glAccount | string | required | — |
| projectCode | string | optional | — |
| currency | string | required | — |
| allocatedAmount | decimal | required, >= 0 | — |
| costType | string | required, one of `CAPEX, OPEX` | — |

#### Entity: BudgetCommitment
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| budgetId | string | required | references Budget.id |
| requestId | string | required | references Request.id |
| committedAmount | decimal | required, > 0 | — |
| status | string | required, one of `reserved, released, consumed` | — |

#### Entity: BudgetException
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| shortfall | decimal | required, > 0 | — |
| reason | string | required | — |
| status | string | required, one of `pending_approval, approved, rejected` | — |

#### Entity: Requisition
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| reference | string | required, unique (e.g. `REQ-2026-001`) | — |
| requestId | string | required | references Request.id |
| title | string | required | — |
| status | string | required, one of `draft, submitted, validated, budget_hold, approved, awaiting_finalisation, finalised, rejected, cancelled` | — |
| estCost | decimal | required | — |
| finalSpent | decimal | required once finalised | — |
| finalisedOn | date | required once finalised | — |
| notes | string | optional | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | Budget checks are internal to Budgeting Service; no external system call is described. |

### Open Decisions (TBD)
- Exact ownership, thresholds and exception-ageing rules for the budget-exception workflow are unconfirmed (carried from business_kb.md).
- The Budget screen's "Plan & Setup," "Spend Papers," "Activity" and "FX" tabs have no captured field/behaviour detail in either source (carried from ui_ux.md Open Questions) — no endpoint is designed for them here.

---

## Module: M3 — Approval & Governance Gate

### Feature Trace

#### Feature: Multi-Level Approval Routing & Segregation of Duties
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| Check outsourcing / single-source / emergency declaration on Governance step | `onDeclarationToggle` | `PATCH /api/v1/requests/{id}/governance-declarations` | Approval / Governance Service | GovernanceDeclaration |
| Fill People & Duties role-assignment fields | `onAssignRoles` | `PATCH /api/v1/requests/{id}/role-assignments` | Approval / Governance Service | RoleAssignment |
| n/a (backend-only) — SoD conflict check | `checkSoDConflict` | `POST /api/v1/requests/{id}/role-assignments/validate` | Approval / Governance Service | RoleAssignment |
| n/a (backend-only) — dynamic approval routing after submit | `routeApproval` | `POST /api/v1/approvals/route` | Approval / Governance Service | ApprovalTask |
| Click "Approve" on Workspace — Approval Inbox row | `onApprove` | `POST /api/v1/approvals/{taskId}/decision` | Approval / Governance Service | ApprovalTask |
| Filter Approval Inbox by Status / Request Type, search | `onFilterInbox` | `GET /api/v1/approvals?status={s}&requestType={t}&search={q}` | Approval / Governance Service | ApprovalTask |

**Ticketing Hints:** stack: dotnet | likely scope: `src/Procurement.Api/Controllers/ApprovalsController.cs`, `src/Procurement.Api/Controllers/GovernanceController.cs` — paired with stack: frontend for the Governance / People & Duties / Approval Inbox screens, linked via `depends_on` | likely scope: `src/features/governance/`, `src/features/approval-inbox/`

**Technical Acceptance Criteria:**
- [ ] `PATCH /api/v1/requests/{id}/role-assignments` returns 422 when the same user name is assigned to more than one of `procurementLead, technicalEvaluator, commercialEvaluator, approver`.
- [ ] `POST /api/v1/approvals/{taskId}/decision` returns 409 when the task's status is not `pending`.
- [ ] `POST /api/v1/approvals/{taskId}/decision` returns 403 when the caller is not the task's assigned approver and holds no active delegation for it.
- [ ] `POST /api/v1/approvals/route` returns 200 with an empty `gates` array when no CTO/CFO/SMC/Board gate applies, rather than erroring.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| PATCH | /api/v1/requests/{id}/governance-declarations | bearer token | `{outsourcing, singleSource, emergency, singleSourceCategory?, singleSourceReason?, emergencyReason?, disruptionImpact?, antiSplitJustification?}` | `{id, ...updated}` | 401 unauthenticated, 404 not found, 422 required follow-up field missing |
| PATCH | /api/v1/requests/{id}/role-assignments | bearer token | `{technicalContact, procurementLead, technicalEvaluator, commercialEvaluator, approver}` | `{id, ...updated, sodConflict: boolean}` | 401 unauthenticated, 404 not found, 422 SoD conflict |
| POST | /api/v1/approvals/route | bearer token | `{requestId}` | `{gates: [{gate: "CTO"\|"CFO"\|"SMC"\|"Board", approverId}]}` | 401 unauthenticated, 404 request not found |
| GET | /api/v1/approvals | bearer token | query: `status, requestType, search, page, pageSize` | `{items: ApprovalTask[], page, pageSize, total}` | 401 unauthenticated |
| POST | /api/v1/approvals/{taskId}/decision | bearer token | `{decision: "approve"\|"reject", comments?}` | `{id, status, decision}` | 401 unauthenticated, 403 not the assigned approver, 404 not found, 409 already decided |

### Data Model

#### Entity: GovernanceDeclaration
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| outsourcing | boolean | required | — |
| singleSource | boolean | required | — |
| emergency | boolean | required | — |
| singleSourceCategory | string | required if singleSource | — |
| singleSourceReason | string | required if singleSource | — |
| emergencyReason | string | required if emergency | — |
| disruptionImpact | string | required if emergency | — |
| antiSplitJustification | string | required if an anti-split alert is present | — |

#### Entity: RoleAssignment
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required, unique | references Request.id |
| technicalContact | string | optional | — |
| procurementLead | string | required | — |
| technicalEvaluator | string | required | — |
| commercialEvaluator | string | required | — |
| approver | string | required | — |

#### Entity: ApprovalTask
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| gate | string | required, one of `CTO, CFO, SMC, Board, Department, ETC/GPPC, GPPC/EXCO, EXCO` | — |
| approverId | string | required | references User.id |
| policyVersion | string | required | — |
| status | string | required, one of `pending, approved, rejected` | — |
| decisionComments | string | optional | — |
| decidedAt | datetime (UTC) | required once decided | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | Approval routing and SoD checks are internal; no external system call is described. |

### Open Decisions (TBD)
- No screen for CTO/CFO/SMC/Board decisioning itself is shown in either UI source (carried from ui_ux.md Open Questions); the `ApprovalTask`/decision endpoints above are designed generically to cover all four gates pending a dedicated screen.
- Exact ownership, thresholds and system-of-record boundaries for each approval gate are unconfirmed (carried from business_kb.md).

---

## Module: M4 — Sourcing Strategy & Supplier Shortlisting

### Feature Trace

#### Feature: Sourcing Route Selection & Category Strategy
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — sourcing route recommendation | `recommendSourcingRoute` | `POST /api/v1/sourcing/route-recommendation` | Sourcing Service | SourcingStrategy |
| n/a (backend-only) — vendor 3-point screening | `screenVendor` | `POST /api/v1/suppliers/{id}/three-point-check` | Sourcing Service + Supplier Service | ThreePointCheck |
| View Procurement Triage Overview screen (assign-to-team, priority, decision) | `onTriageDecision` | `POST /api/v1/sourcing/strategies/{id}/agree` | Sourcing Service | SourcingStrategy |
| n/a (backend-only) — demand consolidation / tail-spend analysis | `analyzeSpend` | `GET /api/v1/sourcing/spend-analysis?category={c}` | Sourcing Service | SpendAnalysis |

**Ticketing Hints:** stack: dotnet | likely scope: `src/Procurement.Api/Controllers/SourcingController.cs` — paired with stack: frontend for the Procurement Triage Overview screen, linked via `depends_on` | likely scope: `src/features/procurement-triage/`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/sourcing/route-recommendation` returns 422 when `estimatedValue` is missing or non-positive.
- [ ] `POST /api/v1/sourcing/strategies/{id}/agree` returns 409 when the strategy is already `agreed`.
- [ ] `POST /api/v1/suppliers/{id}/three-point-check` returns 404 when the supplier does not exist.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/sourcing/route-recommendation | bearer token | `{requestId, estimatedValue, currency, country, singleSource, emergency}` | `{recommendedRoute: "RFQ"\|"RFP"\|"SingleSource"\|"Renewal", minQuotations, requiresTender, requiresGspReroute}` | 401 unauthenticated, 422 missing/invalid estimatedValue |
| POST | /api/v1/suppliers/{id}/three-point-check | bearer token | — | `{identityVerified, registrationEvidenceVerified, activeStatus}` | 401 unauthenticated, 404 supplier not found |
| POST | /api/v1/sourcing/strategies/{id}/agree | bearer token | `{requesterConfirmation: true}` | `{id, status: "agreed"}` | 401 unauthenticated, 404 not found, 409 already agreed |
| GET | /api/v1/sourcing/spend-analysis | bearer token | query: `category` | `{historicalSpend, benchmarkPrice, priceVariancePct, tailSpendFlag}` | 401 unauthenticated |

### Data Model

#### Entity: SourcingStrategy
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| recommendedRoute | string | required, one of `RFQ, RFP, SingleSource, Renewal` | — |
| marketApproach | string | optional | — |
| evaluationMethod | string | optional | — |
| status | string | required, one of `draft, agreed` | — |

#### Entity: ThreePointCheck
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| supplierId | string | required | references Supplier.id |
| identityVerified | boolean | required | — |
| registrationEvidenceVerified | boolean | required | — |
| activeStatus | boolean | required | — |
| checkedAt | datetime (UTC) | required | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | Sourcing-strategy and screening logic are internal; the source names Maybank's market-intelligence/spend-cube layer as a benchmark, not a confirmed integration. |

### Open Decisions (TBD)
- No screen for M4 sourcing-strategy preparation (market overview, spend cube, tail-spend analysis, demand consolidation) exists in either UI source (carried from ui_ux.md); endpoints above are designed generically pending that screen's design.

---

## Module: M5 — Supplier Onboarding, Due Diligence & Lifecycle Management

### Feature Trace

#### Feature: Supplier Registration, Screening & Risk Scoring
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| Choose "Existing registered supplier" on Supplier step | `onSelectExistingSupplier` | `GET /api/v1/suppliers/{id}/status` | Supplier Service | Supplier, DueDiligenceRecord |
| Choose "New / non-registered supplier", fill name/registration, submit reason | `onSubmitNonRegisteredSupplier` | `POST /api/v1/suppliers/duplicate-check` then `POST /api/v1/requests/{id}/supplier-exception` | Supplier Service | Supplier, SupplierException |
| Upload supplier-exception / registration evidence | `onFilesAdded` | `POST /api/v1/requests/{id}/attachments` (documentType=supplierException\|supplierRegistration) | Supplier Service | RequestAttachment |
| View Due Diligence step assessment tiles (3PC/ESG/Associated Person/TPRM) | `onLoadDueDiligence` | `GET /api/v1/requests/{id}/due-diligence` | Supplier Service | DueDiligenceRecord |
| Change Sourcing Type / Spend Type selects | `onChangeApplicability` | `PATCH /api/v1/requests/{id}/due-diligence-applicability` | Supplier Service | DueDiligenceRecord |
| Supplier self-service registration / profile update (named, not screenshotted) | `onSupplierSelfServiceSubmit` | `POST /api/v1/suppliers` / `PATCH /api/v1/suppliers/{id}` | Supplier Service | Supplier |

**Ticketing Hints:** stack: dotnet | likely scope: `src/Procurement.Api/Controllers/SuppliersController.cs`, `src/Procurement.Api/Controllers/DueDiligenceController.cs` — paired with stack: frontend for the Supplier / Due Diligence steps, linked via `depends_on` | likely scope: `src/features/supplier-status/`, `src/features/due-diligence/`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/suppliers/duplicate-check` returns `{duplicateFound: true, candidates: [...]}` (200, not an error) rather than blocking the call itself when a name/registration/bank-account/tax-ID match is found.
- [ ] `POST /api/v1/requests/{id}/supplier-exception` returns 422 when `reason` is empty or no Procurement Head approval evidence is attached.
- [ ] `PATCH /api/v1/suppliers/{id}/bank-details` (sensitive change) returns 202-equivalent `{status: "pending_verification"}` rather than applying immediately.
- [ ] `GET /api/v1/requests/{id}/due-diligence` returns `ready: false` when the supplier is Material and any of TPRM/materialSupplierForm/materialSupplierDocs is missing.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/suppliers | bearer token | `{legalEntityName, registrationNumber, taxId, address, contacts, bankDetails, paymentTerms, categoryCodes}` | `{id, status: "pending"}` | 401 unauthenticated, 422 missing required field |
| GET | /api/v1/suppliers/{id}/status | bearer token | — | `{active, threePCStatus, esgScore, associatedPersonStatus, materialityStatus}` | 401 unauthenticated, 404 not found |
| POST | /api/v1/suppliers/duplicate-check | bearer token | `{legalEntityName, registrationNumber, bankAccountNumber?, contactEmail?}` | `{duplicateFound, candidates: Supplier[]}` | 401 unauthenticated, 422 missing legalEntityName |
| POST | /api/v1/requests/{id}/supplier-exception | bearer token | `{reason, procurementHeadApprovalAttachmentId}` | `{id, status: "pending_approval"}` | 401 unauthenticated, 404 not found, 422 missing reason/evidence |
| PATCH | /api/v1/suppliers/{id}/bank-details | bearer token | `{beneficiaryName, bankName, accountNumber}` | `{id, status: "pending_verification"}` | 401 unauthenticated, 404 not found, 422 missing field |
| GET | /api/v1/requests/{id}/due-diligence | bearer token | — | `{sourcingType, spendType, threePCRequired, esgRequired, materialityStatus, ready}` | 401 unauthenticated, 404 not found |
| PATCH | /api/v1/requests/{id}/due-diligence-applicability | bearer token | `{sourcingType, spendType}` | `{...updated}` | 401 unauthenticated, 404 not found, 422 invalid enum value |

### Data Model

#### Entity: Supplier
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| legalEntityName | string | required | — |
| registrationNumber | string | required, unique | — |
| taxId | string | required | — |
| address | string | required | — |
| bankDetails | object | required, `bankVerified: boolean` sub-field | — |
| paymentTerms | string | optional | — |
| categoryCodes | string[] | required | — |
| kycStatus | string | required, one of `pending, completed, failed` | — |
| esgScore | integer | 0–100 | — |
| spendTier | string | optional | — |
| activeFlag | boolean | required | — |

#### Entity: DueDiligenceRecord
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| supplierId | string | required | references Supplier.id |
| sourcingType | string | required, one of `Sourceable, Non-Sourceable` | — |
| spendType | string | required, one of `Addressable Spend, Non-Addressable Spend` | — |
| threePCStatus | string | required when threePCRequired, one of `Valid, Invalid, Pending` | — |
| esgScore | integer | required when esgRequired | — |
| associatedPersonStatus | string | required, one of `Completed, Pending` | — |
| tprmStatus | string | required when Material, one of `Completed, Pending` | — |
| materialityStatus | string | required, one of `Material, Non-Material, To be assessed` | — |
| ready | boolean | computed | — |

#### Entity: SupplierException
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| reason | string | required | — |
| procurementHeadApprovalAttachmentId | string | required | references RequestAttachment.id |
| status | string | required, one of `pending_approval, approved, rejected` | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | KYC/ESG/financial-health data sources are named conceptually ("Financial Health Check Integration") but no specific external vendor/API is stated. |

### Open Decisions (TBD)
- The source names a "Financial Health Check Integration" system feature (S.5.8) without naming the actual external data provider — flag for `detect-mcp-requirements` and for human confirmation of the specific vendor.
- No screen for the M5 supplier self-service onboarding portal is present in either UI source; the `POST /api/v1/suppliers` / `PATCH /api/v1/suppliers/{id}` endpoints above are designed generically pending that screen's design.

---

## Module: M6 — RFx Design, Event Administration & Submission

### Feature Trace

#### Feature: RFx Template Build, Publication & Submission Control
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — no screen captured in either source | `buildRfx` | `POST /api/v1/rfx-events` | RFx / Sourcing Event Service | RfxEvent |
| n/a (backend-only) | `publishRfx` | `POST /api/v1/rfx-events/{id}/publish` | RFx / Sourcing Event Service | RfxEvent |
| n/a (backend-only) | `inviteSuppliers` | `POST /api/v1/rfx-events/{id}/invitations` | RFx / Sourcing Event Service | RfxInvitation |
| n/a (backend-only) | `extendDeadline` | `POST /api/v1/rfx-events/{id}/extend` | RFx / Sourcing Event Service | RfxEvent |
| n/a (backend-only) | `submitProposal` (supplier-facing, out of Etiqa-user scope) | `POST /api/v1/rfx-events/{id}/submissions` | RFx / Sourcing Event Service | RfxSubmission |
| n/a (backend-only) | `openProposals` | `POST /api/v1/rfx-events/{id}/open` | RFx / Sourcing Event Service | RfxEvent, RfxSubmission |

**Ticketing Hints:** stack: dotnet | likely scope: `src/Procurement.Api/Controllers/RfxEventsController.cs` — paired with stack: frontend (M14, no source UI existed so the screen was designed fresh) | likely scope: `src/features/rfx-events/`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/rfx-events/{id}/publish` returns 422 when required sections (technical, commercial, evaluators, dates) are incomplete.
- [ ] `POST /api/v1/rfx-events/{id}/submissions` returns 409 when submitted after the closing time and no late-submission exception is recorded.
- [ ] `POST /api/v1/rfx-events/{id}/open` returns 403 when called before the submission deadline by a non-authorised opener.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| GET | /api/v1/rfx-events | bearer token | query: `status?, page?, pageSize?` | `PagedResponse<RfxEventResponse>` | 401 unauthenticated |
| GET | /api/v1/rfx-events/{id} | bearer token | — | `RfxEventDetailResponse` (adds `invitedSupplierIds`, `submissions`) | 401 unauthenticated, 404 not found |
| POST | /api/v1/rfx-events | bearer token | `{sourcingStrategyId, technicalTemplateId, commercialTemplateId, contractTemplateId, tenderType: "open"\|"invited"}` | `{id, status: "draft"}` | 401 unauthenticated, 422 missing required template |
| POST | /api/v1/rfx-events/{id}/publish | bearer token | — | `{id, status: "published", openingDate, closingDate}` | 401 unauthenticated, 404 not found, 422 incomplete configuration |
| POST | /api/v1/rfx-events/{id}/invitations | bearer token | `{supplierIds: string[]}` | `{invited: string[]}` | 401 unauthenticated, 404 event not found, 422 supplier not eligible |
| POST | /api/v1/rfx-events/{id}/extend | bearer token | `{newClosingDate, reason}` | `{id, closingDate}` | 401 unauthenticated, 404 not found, 422 missing reason |
| POST | /api/v1/rfx-events/{id}/submissions | supplier token (external) | `{technicalProposal, commercialProposal, attachments}` | `{id, submittedAt, bidStatus}` | 401 unauthenticated, 404 not found, 409 late without exception |
| POST | /api/v1/rfx-events/{id}/open | bearer token | — | `{openedBy, openedAt, submissions: RfxSubmission[]}` | 401 unauthenticated, 403 before deadline, 404 not found |

**Deviation (M14):** the GET list/detail rows above were not in the original listing — no read endpoint existed at all, which blocks any frontend from ever selecting an RFx event to act on. Added when building the M14 frontend; see `RfxEventsController.cs` and `ui_ux.md` Module M6.

### Data Model

#### Entity: RfxEvent
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| sourcingStrategyId | string | required | references SourcingStrategy.id |
| tenderType | string | required, one of `open, invited` | — |
| status | string | required, one of `draft, published, closed, cancelled` | — |
| openingDate | datetime (UTC) | required once published | — |
| closingDate | datetime (UTC) | required once published | — |

#### Entity: RfxSubmission
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| rfxEventId | string | required | references RfxEvent.id |
| supplierId | string | required | references Supplier.id |
| bidStatus | string | required, one of `submitted, late, opened, disqualified` | — |
| submittedAt | datetime (UTC) | required | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | RFx administration is internal; no external system is named. |

### Open Decisions (TBD)
- ~~No screen exists in either UI source for RFx authoring/publishing~~ — resolved by M14 (Jira epic SMOKETEST-121): `frontend/src/features/rfx-events/RfxEvents.tsx`, an original design (no source mockup existed) documented in `ui_ux.md` Module M6. `submitProposal` stays backend-only/supplier-facing, out of this internal app's scope.

---

## Module: M7 — Technical, Commercial & Contract Evaluation

### Feature Trace

#### Feature: Structured Evaluation, Clarification & Status Gate
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — no screen captured | `scoreEvaluation` | `POST /api/v1/rfx-events/{id}/evaluations` | RFx / Sourcing Event Service | Evaluation |
| n/a (backend-only) | `raiseClarification` | `POST /api/v1/rfx-events/{id}/clarifications` | RFx / Sourcing Event Service | Clarification |
| n/a (backend-only) | `resolveClarification` | `PATCH /api/v1/clarifications/{id}` | RFx / Sourcing Event Service | Clarification |
| n/a (backend-only) | `decideStatusGate` | `POST /api/v1/rfx-events/{id}/status-gate-decision` | RFx / Sourcing Event Service | Evaluation |

**Ticketing Hints:** stack: dotnet (backend-only; no UI in either source) | likely scope: `src/Procurement.Api/Controllers/EvaluationsController.cs`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/rfx-events/{id}/evaluations` returns 409 when the evaluation is already `locked`.
- [ ] `POST /api/v1/rfx-events/{id}/status-gate-decision` returns 422 when an unresolved material clarification/deviation exists.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/rfx-events/{id}/evaluations | bearer token | `{supplierId, dimension: "technical"\|"commercial"\|"contract", score, comments}` | `{id, status: "locked"}` | 401 unauthenticated, 404 not found, 409 already locked |
| POST | /api/v1/rfx-events/{id}/clarifications | bearer token | `{supplierId, question, dueDate}` | `{id, status: "open"}` | 401 unauthenticated, 404 not found |
| PATCH | /api/v1/clarifications/{id} | bearer token | `{reply?, status: "closed"\|"open"}` | `{id, ...updated}` | 401 unauthenticated, 404 not found |
| POST | /api/v1/rfx-events/{id}/status-gate-decision | bearer token | `{supplierId, decision: "proceed"\|"stop", reason?}` | `{id, decision}` | 401 unauthenticated, 404 not found, 422 unresolved material item |

### Data Model

#### Entity: Evaluation
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| rfxEventId | string | required | references RfxEvent.id |
| supplierId | string | required | references Supplier.id |
| dimension | string | required, one of `technical, commercial, contract` | — |
| score | decimal | required | — |
| status | string | required, one of `open, locked` | — |

#### Entity: Clarification
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| rfxEventId | string | required | references RfxEvent.id |
| supplierId | string | required | references Supplier.id |
| question | string | required | — |
| reply | string | optional | — |
| status | string | required, one of `open, closed` | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | Evaluation/clarification workflow is internal. |

### Open Decisions (TBD)
- No evaluation workspace UI is described in either source (carried from ui_ux.md).

---

## Module: M8 — Negotiation, eAuction, BAFO & Consolidation

### Feature Trace

#### Feature: Negotiation Rounds, Award Scenario Comparison & Savings Calculation
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — no screen captured | `recordNegotiationRound` | `POST /api/v1/rfx-events/{id}/negotiation-rounds` | RFx / Sourcing Event Service | NegotiationRound |
| n/a (backend-only) | `requestBafo` | `POST /api/v1/rfx-events/{id}/bafo-requests` | RFx / Sourcing Event Service | NegotiationRound |
| n/a (backend-only) | `consolidateEvaluation` | `GET /api/v1/rfx-events/{id}/consolidated-result` | RFx / Sourcing Event Service | Evaluation, NegotiationRound |
| n/a (backend-only) | `calculateSavings` | `POST /api/v1/rfx-events/{id}/savings-calculation` | RFx / Sourcing Event Service | SavingsRecord |

**Ticketing Hints:** stack: dotnet (backend-only; no UI in either source) | likely scope: `src/Procurement.Api/Controllers/NegotiationController.cs`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/rfx-events/{id}/savings-calculation` returns 422 when no approved baseline value is available for comparison.
- [ ] `GET /api/v1/rfx-events/{id}/consolidated-result` returns 409 when any supplier's evaluation dimension is not yet `locked`.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/rfx-events/{id}/negotiation-rounds | bearer token | `{supplierId, revisedPrice, terms, roundType: "negotiation"\|"eAuction"}` | `{id, roundNumber}` | 401 unauthenticated, 404 not found |
| POST | /api/v1/rfx-events/{id}/bafo-requests | bearer token | `{supplierId, deadline}` | `{id, status: "requested"}` | 401 unauthenticated, 404 not found |
| GET | /api/v1/rfx-events/{id}/consolidated-result | bearer token | — | `{supplierId, totalScore, recommendedScenario}[]` | 401 unauthenticated, 404 not found, 409 evaluation not locked |
| POST | /api/v1/rfx-events/{id}/savings-calculation | bearer token | `{baselineValue, awardedValue, method: "hard"\|"soft"}` | `{savingsAmount, savingsType, financeValidated: false}` | 401 unauthenticated, 404 not found, 422 missing baseline |

### Data Model

#### Entity: NegotiationRound
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| rfxEventId | string | required | references RfxEvent.id |
| supplierId | string | required | references Supplier.id |
| roundNumber | integer | required | — |
| revisedPrice | decimal | required | — |
| roundType | string | required, one of `negotiation, eAuction, bafo` | — |

#### Entity: SavingsRecord
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| rfxEventId | string | required | references RfxEvent.id |
| baselineValue | decimal | required | — |
| awardedValue | decimal | required | — |
| savingsAmount | decimal | computed | — |
| savingsType | string | required, one of `hard, soft` | — |
| financeValidated | boolean | required, default false | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | Negotiation/eAuction workflow is internal. |

### Open Decisions (TBD)
- No negotiation/eAuction UI is described in either source (carried from ui_ux.md).

---

## Module: M9 — Award, Due Diligence & Contract Execution

### Feature Trace

#### Feature: Award Recommendation & Final Due Diligence
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — no screen captured | `prepareAwardPack` | `POST /api/v1/awards` | Contract Management | Award |
| n/a (backend-only) | `runFinalDueDiligence` | `POST /api/v1/awards/{id}/final-due-diligence` | Supplier Service | DueDiligenceRecord |

**Ticketing Hints:** stack: dotnet (backend-only) | likely scope: `src/Procurement.Api/Controllers/AwardsController.cs`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/awards/{id}/final-due-diligence` returns 422 when due diligence is not `ready` and no exemption is recorded.

#### Feature: Contract Lifecycle Management, Repository & Catalogue
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — no screen captured | `draftContract` | `POST /api/v1/contracts` | Contract Management | Contract |
| n/a (backend-only) | `signContract` | `POST /api/v1/contracts/{id}/sign` | Contract Management | Contract |
| n/a (backend-only) | `publishPricebookLine` | `POST /api/v1/contracts/{id}/pricebook-lines` | Contract Management | PricebookLine |
| n/a (backend-only) | `issueAgmtId` | `POST /api/v1/contracts/{id}/agmt` | Contract Management | Contract |

**Ticketing Hints:** stack: dotnet (backend-only; no UI in either source) | likely scope: `src/Procurement.Api/Controllers/ContractsController.cs`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/contracts/{id}/sign` returns 403 when the signatory's authority limit is below the contract value.
- [ ] `POST /api/v1/contracts/{id}/agmt` returns 409 when the contract is not `signed`.
- [ ] `POST /api/v1/contracts/{id}/pricebook-lines` returns 422 when the contract is `expired`.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/awards | bearer token | `{rfxEventId, consolidatedResultRef, recommendedSupplierId, scenario}` | `{id, status: "pending_approval"}` | 401 unauthenticated, 422 missing recommendedSupplierId |
| POST | /api/v1/awards/{id}/final-due-diligence | bearer token | `{exemptionReason?, exemptionApproverId?}` | `{id, ready, exemptionRecorded}` | 401 unauthenticated, 404 not found, 422 not ready and no exemption |
| POST | /api/v1/contracts | bearer token | `{awardId, templateId}` | `{id, status: "drafting"}` | 401 unauthenticated, 404 award not found |
| POST | /api/v1/contracts/{id}/sign | bearer token | `{signatoryId}` | `{id, status: "signed", executionDate}` | 401 unauthenticated, 403 signatory limit exceeded, 404 not found |
| POST | /api/v1/contracts/{id}/pricebook-lines | bearer token | `{itemDescription, sku, unitPrice, uom, currency, validFrom, validTo}` | `{id}` | 401 unauthenticated, 404 not found, 422 contract expired |
| POST | /api/v1/contracts/{id}/agmt | bearer token | — | `{agmtId}` | 401 unauthenticated, 404 not found, 409 contract not signed |

### Data Model

#### Entity: Award
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| rfxEventId | string | required | references RfxEvent.id |
| recommendedSupplierId | string | required | references Supplier.id |
| status | string | required, one of `pending_approval, approved, rejected` | — |

#### Entity: Contract
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| agmtId | string | unique, issued once signed | — |
| awardId | string | required | references Award.id |
| supplierId | string | required | references Supplier.id |
| versionNumber | integer | required | — |
| status | string | required, one of `drafting, redlining, signed, active, expired, terminated` | — |
| executionDate | date | required once signed | — |
| effectiveStart | date | required | — |
| effectiveEnd | date | required | — |

#### Entity: PricebookLine
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| contractId | string | required | references Contract.id |
| sku | string | required | — |
| unitPrice | decimal | required | — |
| uom | string | required | — |
| validFrom | date | required | — |
| validTo | date | required | — |

### Integrations Needed
| System | Reason |
|---|---|
| none identified for this module | Contract signing/repository is internal; no e-signature vendor is named by the source (flagged for `detect-mcp-requirements`). |

### Open Decisions (TBD)
- No award/contract UI is described in either source (carried from ui_ux.md).
- Whether digital signing uses an external e-signature provider is not stated.

---

## Module: M10 — PR/PO, Delivery & Supplier e-Invoice 3-Way Match

### Feature Trace

#### Feature: PR/PO Creation, Delivery Acceptance & Invoice 3-Way Match
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| Click "New Requisition" (Workspace — Requisitions / Dashboard tile) | `onCreateRequisition` | `POST /api/v1/purchase-requisitions` | Procurement Service | PurchaseRequisition |
| Filter/search Requisitions table (Status, Category) | `onFilterRequisitions` | `GET /api/v1/purchase-requisitions?status={s}&category={c}` | Procurement Service | PurchaseRequisition |
| n/a (backend-only) — PR approval and PO issue | `approvePrIssuePo` | `POST /api/v1/purchase-requisitions/{id}/approve` | Procurement Service | PurchaseRequisition, PurchaseOrder |
| n/a (backend-only) — contract/price validity check | `checkContractPriceValidity` | `GET /api/v1/contracts/{id}/pricebook-lines/{sku}/validity` | Contract Management | PricebookLine |
| n/a (backend-only) — record delivery/GR | `recordDelivery` | `POST /api/v1/purchase-orders/{id}/goods-receipts` | ePV | GoodsReceipt |
| n/a (backend-only) — capture supplier e-Invoice/MyInvois | `captureInvoice` | `POST /api/v1/invoices` | ePV / MyInvois | Invoice |
| n/a (backend-only) — auto-create ePV voucher | `createEpvVoucher` | `POST /api/v1/epv-vouchers` | ePV | EpvVoucher |
| n/a (backend-only) — 3-way match | `runThreeWayMatch` | `POST /api/v1/epv-vouchers/{id}/match` | ePV | EpvVoucher |
| n/a (backend-only) — resolve mismatch | `resolveMismatch` | `PATCH /api/v1/epv-vouchers/{id}/exceptions/{exceptionId}` | ePV | InvoiceException |

**Ticketing Hints:** stack: dotnet | likely scope: `src/Procurement.Api/Controllers/PurchaseRequisitionsController.cs`, `src/Procurement.Api/Controllers/EpvVouchersController.cs` (backend) — paired with stack: frontend for the Requisitions list/create UI, linked via `depends_on` | likely scope: `src/features/requisitions/`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/purchase-requisitions/{id}/approve` returns 409 when the linked contract/AGMT is expired or the item price differs from the pricebook.
- [ ] `POST /api/v1/epv-vouchers/{id}/match` returns 200 with `matchStatus: "mismatch"` (not an error) and the exact quantity/price/amount difference when outside tolerance.
- [ ] `POST /api/v1/invoices` returns 409 when supplier/invoiceNumber/amount/currency/PO matches an existing invoice (duplicate).
- [ ] `PATCH /api/v1/epv-vouchers/{id}/exceptions/{exceptionId}` returns 422 when `resolution` is not one of `correction, dispute, credit_note, debit_note, refund_note`.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/purchase-requisitions | bearer token | `{requestId, awardId?, contractId?, items: [{itemDescription, quantity, unitPrice, costCentre, glAccount}]}` | `{id, status: "draft"}` | 401 unauthenticated, 422 missing items |
| GET | /api/v1/purchase-requisitions | bearer token | query: `status, category, page, pageSize` | `{items: PurchaseRequisition[], page, pageSize, total}` | 401 unauthenticated |
| POST | /api/v1/purchase-requisitions/{id}/approve | bearer token | — | `{id, status: "approved", poNumber}` | 401 unauthenticated, 404 not found, 409 contract expired / price mismatch |
| POST | /api/v1/purchase-orders/{id}/goods-receipts | bearer token | `{quantityReceived, acceptanceDate, acceptedBy}` | `{id, grn}` | 401 unauthenticated, 404 PO not found |
| POST | /api/v1/invoices | bearer token | `{supplierId, invoiceNumber, invoiceDate, amount, tax, poNumber, grn, myInvoisReference?}` | `{id, invoiceType}` | 401 unauthenticated, 409 duplicate invoice, 422 missing MyInvois reference when required |
| POST | /api/v1/epv-vouchers | bearer token | `{poNumber, grn, invoiceId}` | `{id, status: "pending_match"}` | 401 unauthenticated, 404 referenced record not found |
| POST | /api/v1/epv-vouchers/{id}/match | bearer token | — | `{matchStatus: "matched"\|"mismatch", differences?}` | 401 unauthenticated, 404 not found |
| PATCH | /api/v1/epv-vouchers/{id}/exceptions/{exceptionId} | bearer token | `{owner, resolution, adjustmentNote?}` | `{id, status: "resolved"}` | 401 unauthenticated, 404 not found, 422 invalid resolution |
| POST | /api/v1/epv-vouchers/{id}/approve | bearer token | — | `{id, status: "ready_for_payment"}` | 401 unauthenticated, 404 not found, 409 not matched |

### Data Model

#### Entity: PurchaseRequisition
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| contractId | string | optional | references Contract.id |
| route | string | required, one of `catalogue, non_catalogue, manual_po` | — |
| status | string | required, one of `draft, approved, rejected` | — |
| poNumber | string | unique, set on approval | — |

#### Entity: PurchaseOrder
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| purchaseRequisitionId | string | required | references PurchaseRequisition.id |
| supplierId | string | required | references Supplier.id |
| poDate | date | required | — |
| amount | decimal | required | — |

#### Entity: GoodsReceipt
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| purchaseOrderId | string | required | references PurchaseOrder.id |
| grn | string | required, unique | — |
| quantityReceived | decimal | required | — |
| acceptanceDate | date | required | — |
| acceptedBy | string | required | — |

#### Entity: Invoice
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| supplierId | string | required | references Supplier.id |
| invoiceNumber | string | required | — |
| invoiceDate | date | required | — |
| amount | decimal | required | — |
| tax | decimal | required | — |
| poNumber | string | required | references PurchaseOrder.id |
| grn | string | required | references GoodsReceipt.grn |
| myInvoisReference | string | required when applicable | — |
| invoiceType | string | required, one of `standard, self_billed_pending, exempt` | — |

#### Entity: EpvVoucher
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| poNumber | string | required | references PurchaseOrder.id |
| grn | string | required | references GoodsReceipt.grn |
| invoiceId | string | required | references Invoice.id |
| status | string | required, one of `pending_match, matched, mismatch, ready_for_payment, approved` | — |

#### Entity: InvoiceException
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| epvVoucherId | string | required | references EpvVoucher.id |
| owner | string | required | — |
| resolution | string | required, one of `correction, dispute, credit_note, debit_note, refund_note` | — |
| status | string | required, one of `open, resolved` | — |

### Integrations Needed
| System | Reason |
|---|---|
| MyInvois | Supplier e-Invoice capture requires a MyInvois unique reference/QR/validation status where required (LHDN e-Invoicing). |

### Open Decisions (TBD)
- Exact ownership, thresholds and system-of-record boundaries for this module are unconfirmed (carried from business_kb.md).
- No screen for supplier e-Invoice/MyInvois capture or ePV/3-way-match exception handling is present in either UI source (carried from ui_ux.md).

---

## Module: M11 — Payment, Self-Billed e-Invoice, GL Posting & Bank Reconciliation

### Feature Trace

#### Feature: Payment Instruction, Approval & Execution
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — payment route selection | `selectPaymentRoute` | `POST /api/v1/payments` | Payout | Payment |
| n/a (backend-only) — tax/WHT/SST treatment | `applyTaxTreatment` | `POST /api/v1/payments/{id}/tax-treatment` | Payout | Payment |
| n/a (backend-only) — payment approval / SoD | `approvePayment` | `POST /api/v1/payments/{id}/approve` | Payout | Payment |
| n/a (backend-only) — beneficiary/duplicate/limit checks | `runPaymentControls` | `POST /api/v1/payments/{id}/controls-check` | Payout | Payment |
| n/a (backend-only) — execute payout | `executePayout` | `POST /api/v1/payments/{id}/execute` | Payout / Bank | Payment |

**Ticketing Hints:** stack: dotnet (backend-only; the Finance Portal "Payment Vouchers" nav entry has no captured screen content per ui_ux.md) | likely scope: `src/Procurement.Api/Controllers/PaymentsController.cs`

**Technical Acceptance Criteria:**
- [ ] `POST /api/v1/payments/{id}/controls-check` returns 409 when the beneficiary/bank account fails verification, a duplicate payment is detected, or the amount exceeds the approved limit.
- [ ] `POST /api/v1/payments/{id}/approve` returns 422 when the approver has an SoD conflict with the preparer.

#### Feature: Self-Billed MyInvois, GL Posting & Bank Reconciliation
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — self-billed applicability check | `checkSelfBilledApplicability` | `GET /api/v1/payments/{id}/self-billed-applicability` | MyInvois / Self-Billed e-Invoice | SelfBilledRecord |
| n/a (backend-only) — submit self-billed payload | `submitSelfBilled` | `POST /api/v1/payments/{id}/self-billed-submission` | MyInvois / Self-Billed e-Invoice | SelfBilledRecord |
| n/a (backend-only) — receive MyInvois response | `receiveMyInvoisResponse` | webhook `POST /api/v1/myinvois/webhooks/response` (inbound) | MyInvois / Self-Billed e-Invoice | SelfBilledRecord |
| n/a (backend-only) — correct/resubmit rejected record | `resubmitSelfBilled` | `POST /api/v1/self-billed-records/{id}/resubmit` | MyInvois / Self-Billed e-Invoice | SelfBilledRecord |
| n/a (backend-only) — post GL | `postGl` | `POST /api/v1/gl-entries` | GL / Bank Reconciliation | GlEntry |
| n/a (backend-only) — bank reconciliation | `reconcileBank` | `POST /api/v1/bank-records/{id}/match` | GL / Bank Reconciliation | BankRecord |
| n/a (backend-only) — audit pack retention | `assembleAuditPack` | `POST /api/v1/audit-packs` | CCM | AuditPack |

**Ticketing Hints:** stack: dotnet (backend-only; no UI in either source beyond the Finance Portal nav entry name) | likely scope: `src/Procurement.Api/Controllers/SelfBilledController.cs`, `src/Procurement.Api/Controllers/GlController.cs`, `src/Procurement.Api/Controllers/BankReconciliationController.cs`

**Technical Acceptance Criteria:**
- [ ] `GET /api/v1/payments/{id}/self-billed-applicability` returns `{applicable: false}` (200, not an error) for a payment category the source states is not LHDN-defined, rather than attempting submission.
- [ ] `POST /api/v1/bank-records/{id}/match` returns 200 with `matchStatus: "unmatched"` (not an error) when no payment reference matches, and creates a reconciliation exception.
- [ ] `POST /api/v1/gl-entries` returns 422 when debit and credit totals do not balance.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/payments | bearer token | `{epvVoucherId, route: "PO"\|"direct"\|"TT", payeeId, bankDetails, amount, currency, paymentDate}` | `{id, reference, status: "draft"}` | 401 unauthenticated, 422 missing route/payee |
| POST | /api/v1/payments/{id}/tax-treatment | bearer token | `{taxCode, whtAmount?, sstAmount?}` | `{id, payableAmount, taxAmount}` | 401 unauthenticated, 404 not found |
| POST | /api/v1/payments/{id}/approve | bearer token | `{approverId}` | `{id, status: "approved"}` | 401 unauthenticated, 404 not found, 422 SoD conflict |
| POST | /api/v1/payments/{id}/controls-check | bearer token | — | `{beneficiaryVerified, duplicateFlag, withinLimit}` | 401 unauthenticated, 404 not found, 409 control failed |
| POST | /api/v1/payments/{id}/execute | bearer token | — | `{id, status: "executed"\|"failed"\|"pending", bankReference}` | 401 unauthenticated, 404 not found, 409 not approved |
| GET | /api/v1/payments/{id}/self-billed-applicability | bearer token | — | `{applicable}` | 401 unauthenticated, 404 not found |
| POST | /api/v1/payments/{id}/self-billed-submission | bearer token | — | `{id, myInvoisPayloadVersion, submissionDate}` | 401 unauthenticated, 404 not found, 422 not applicable |
| POST | /api/v1/myinvois/webhooks/response | signed webhook | `{selfBilledRecordId, uniqueId, qr, status: "accepted"\|"rejected", rejectionReason?}` | `{received: true}` | 401 invalid signature, 404 record not found |
| POST | /api/v1/self-billed-records/{id}/resubmit | bearer token | `{correctedFields}` | `{id, status: "resubmitted"}` | 401 unauthenticated, 404 not found, 409 not rejected |
| POST | /api/v1/gl-entries | bearer token | `{glAccount, costCentre, entity, debit, credit, tax, wht, postingDate}` | `{id}` | 401 unauthenticated, 422 debit/credit imbalance |
| POST | /api/v1/bank-records | bearer token | `{bankReference, date, amount, currency, status}` | `{id}` | 401 unauthenticated, 422 missing field |
| POST | /api/v1/bank-records/{id}/match | bearer token | `{paymentId}` | `{matchStatus: "matched"\|"unmatched"}` | 401 unauthenticated, 404 not found |
| POST | /api/v1/audit-packs | bearer token | `{requestId}` | `{id, retentionDate}` | 401 unauthenticated, 404 request not found |

### Data Model

#### Entity: Payment
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| reference | string | required, unique (PV/EPRF/BOT number) | — |
| epvVoucherId | string | required | references EpvVoucher.id |
| route | string | required, one of `PO, direct, TT` | — |
| amount | decimal | required | — |
| currency | string | required | — |
| status | string | required, one of `draft, approved, executing, executed, failed, pending` | — |
| bankReference | string | set on execution | — |

#### Entity: SelfBilledRecord
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| paymentId | string | required | references Payment.id |
| status | string | required, one of `not_applicable, pending, accepted, rejected, resubmitted` | — |
| uniqueId | string | set once accepted | — |
| qr | string | set once accepted | — |
| rejectionReason | string | set on rejection | — |

#### Entity: GlEntry
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| paymentId | string | required | references Payment.id |
| glAccount | string | required | — |
| costCentre | string | required | — |
| debit | decimal | required | — |
| credit | decimal | required | — |
| postingDate | date | required | — |
| reversalFlag | boolean | required, default false | — |

#### Entity: BankRecord
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| bankReference | string | required | — |
| paymentId | string | optional (set on match) | references Payment.id |
| matchStatus | string | required, one of `unmatched, matched` | — |

#### Entity: AuditPack
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| requestId | string | required | references Request.id |
| retentionDate | date | required, >= 15 years from creation | — |

### Integrations Needed
| System | Reason |
|---|---|
| MyInvois | Self-billed e-Invoice submission and response handling for LHDN-applicable categories. |
| Bank (payment execution / statement feed) | Payout execution and bank-statement intake for reconciliation; specific bank/channel not named by the source. |
| CCM | Audit-pack evidence retention per the approved records schedule. |

### Open Decisions (TBD)
- Exact ownership, thresholds and system-of-record boundaries for this module are unconfirmed (carried from business_kb.md).
- No screen for Payment Vouchers / self-billed / GL / bank-reconciliation review is present in either UI source beyond the nav entry name (carried from ui_ux.md).

---

## Module: M12 — Post-Contract Supplier Management, Savings & Audit

### Feature Trace

#### Feature: Contract Governance, Supplier Performance & Savings Validation
| UI Action (from ui_ux.md) | Handler | API Call | Backend Service | Data Touched |
|---|---|---|---|---|
| n/a (backend-only) — no screen captured | `trackObligation` | `POST /api/v1/contracts/{id}/obligations` | Contract Management | ContractObligation |
| n/a (backend-only) | `recordDispute` | `POST /api/v1/disputes` | Vendor Management | Dispute |
| n/a (backend-only) | `recordPerformance` | `POST /api/v1/suppliers/{id}/performance-records` | Vendor Management | PerformanceRecord |
| n/a (backend-only) | `validateSavings` | `PATCH /api/v1/savings-records/{id}/finance-validation` | Vendor Management + Finance | SavingsRecord |
| n/a (backend-only) | `startRequalification` | `POST /api/v1/suppliers/{id}/requalify` | Supplier Service | Supplier |
| n/a (backend-only) | `generateP2PAuditPack` | `GET /api/v1/audit-packs/{requestId}/p2p-report` | CCM | AuditPack |

**Ticketing Hints:** stack: dotnet (backend-only; no UI in either source) | likely scope: `src/Procurement.Api/Controllers/ContractGovernanceController.cs`, `src/Procurement.Api/Controllers/DisputesController.cs`

**Technical Acceptance Criteria:**
- [ ] `PATCH /api/v1/savings-records/{id}/finance-validation` returns 422 when called by a non-Finance role.
- [ ] `POST /api/v1/contracts/{id}/obligations` returns 422 when `dueDate` is in the past at creation.
- [ ] `GET /api/v1/audit-packs/{requestId}/p2p-report` returns 404 when no linked request/contract/PO/invoice/payment chain exists.

### REST API Listing
| Method | Path | Auth | Request Schema | Response Schema | Error Cases |
|---|---|---|---|---|---|
| POST | /api/v1/contracts/{id}/obligations | bearer token | `{description, owner, dueDate}` | `{id, status: "open"}` | 401 unauthenticated, 404 not found, 422 dueDate in past |
| POST | /api/v1/disputes | bearer token | `{contractId, poNumber?, grn?, supplierId, disputeType, description, financialImpact}` | `{id, status: "open"}` | 401 unauthenticated, 422 missing description |
| POST | /api/v1/suppliers/{id}/performance-records | bearer token | `{poNumber, incidentDescription, correctiveAction?}` | `{id}` | 401 unauthenticated, 404 supplier not found |
| PATCH | /api/v1/savings-records/{id}/finance-validation | bearer token (Finance) | `{validated: true}` | `{id, financeValidated}` | 401 unauthenticated, 403 not Finance, 404 not found |
| POST | /api/v1/suppliers/{id}/requalify | bearer token | `{trigger: "scheduled"\|"renewal"\|"material_change"}` | `{id, requalificationStatus: "in_progress"}` | 401 unauthenticated, 404 not found |
| GET | /api/v1/audit-packs/{requestId}/p2p-report | bearer token | — | `{request, contract, po, invoice, payment, gl, bankReference, exceptions: [...]}` | 401 unauthenticated, 404 not found |

### Data Model

#### Entity: ContractObligation
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| contractId | string | required | references Contract.id |
| description | string | required | — |
| owner | string | required | — |
| dueDate | date | required, must be >= creation date | — |
| status | string | required, one of `open, overdue, closed` | — |

#### Entity: Dispute
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| contractId | string | required | references Contract.id |
| supplierId | string | required | references Supplier.id |
| disputeType | string | required | — |
| financialImpact | decimal | optional | — |
| resolutionStatus | string | required, one of `open, resolved, escalated` | — |

#### Entity: PerformanceRecord
| Field | Type | Constraints | Relationships |
|---|---|---|---|
| id | string (uuid) | required, unique | — |
| supplierId | string | required | references Supplier.id |
| poNumber | string | required | references PurchaseOrder.id |
| incidentDescription | string | required | — |
| resolutionStatus | string | required, one of `open, resolved` | — |

### Integrations Needed
| System | Reason |
|---|---|
| CCM | End-to-end P2P audit pack/dashboard evidence retention. |

### Open Decisions (TBD)
- No post-contract SRM/KPI dashboard UI is described in either source (carried from ui_ux.md).
- Whether "Existing ETQ" vs. "Recommendation" tagging applies to any specific M12 feature is unconfirmed (carried from business_kb.md's cross-module Open TBD).

---

## Cross-Module Integrations Needed (consolidated)

| System | Reason |
|---|---|
| MyInvois | Supplier e-Invoice capture (M10) and self-billed e-Invoice submission/response (M11) for LHDN-applicable transactions. |
| Bank (payment execution / statement feed) | Payout execution and bank-statement intake for reconciliation (M11); specific bank/channel not named. |
| CCM | Audit-pack evidence retention (M11, M12), per the approved records schedule (>= 15 years). |
| Financial Health Check provider | Named conceptually for supplier financial-health/risk scoring (M5, S.5.8); specific vendor not named. |
| Kafka | Named in the source as the event backbone connecting Finance Portal, Procurement Service, ePV, Payout, CCM and legacy ACP/SAS consumers; not modelled as a per-module integration above since it is infrastructure, not an external system call. |
| ACP / SAS | Named in the source as legacy file-based downstream consumers; specific interface/file format not stated. |

## Open Decisions (TBD) — document-wide

- **Backend stack choice (dotnet vs. Python):** this FSD commits every module to `stack: dotnet` with RFC 7807 `ProblemDetails`, to match what `developer-frontend.md` already assumes; `developer-backend.md` (Python/FastAPI) exists in the same `.loop-eng/agent-templates/` directory but is not used by any ticket in this backlog under that decision. Needs explicit human confirmation before build starts, since it overrides one of two committed developer templates.
- All module-level Open Decisions above (ownership/thresholds/system-of-record validation; Existing-ETQ vs. Recommendation tagging; Maybank-benchmark phasing; Finance-Portal-extend-vs-integrate question; role-model reconciliation; unclear "Miss post contract management" annotation) are carried forward unresolved from business_kb.md and repeated per-module above; they are not re-litigated here.
- No literal authentication/authorization scheme is stated by any source; the bearer-token convention above is a design placeholder, not a confirmed decision.
