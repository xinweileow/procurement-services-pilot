export interface RequestResponse {
  id: string;
  requestId: string;
  requesterId: string;
  category: string;
  estimatedValue: number;
  currency: string;
  title: string;
  status: string;
  routingDestination: string;
  potentialDuplicateOrSplit: boolean;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface CatalogueItemResponse {
  name: string;
  category: string;
  supplier: string;
}

export interface DashboardSummaryResponse {
  budgetHealth: { allocated: number; available: number; reserved: number; spent: number };
  spendTrend: unknown[];
  pendingApprovals: unknown[];
  financeConsole: unknown[];
}

// M14 — RFx Design, Event Administration & Submission (docs/kb/technical_kb.md Module M6)
export interface RfxEventResponse {
  id: string;
  sourcingStrategyId: string;
  tenderType: 'open' | 'invited';
  status: 'draft' | 'published' | 'closed' | 'cancelled' | 'opened';
  openingDateUtc: string | null;
  closingDateUtc: string | null;
  createdAtUtc: string;
}

export interface RfxSubmissionResponse {
  id: string;
  supplierId: string;
  technicalProposal: string | null;
  commercialProposal: string | null;
  bidStatus: 'submitted' | 'late' | 'opened' | 'disqualified';
  submittedAt: string;
}

export interface RfxEventDetailResponse extends RfxEventResponse {
  extensionReason: string | null;
  cancellationReason: string | null;
  openedBy: string | null;
  openedAtUtc: string | null;
  invitedSupplierIds: string[];
  submissions: RfxSubmissionResponse[];
}

// M15 — Technical, Commercial & Contract Evaluation (docs/kb/technical_kb.md Module M7)
export interface EvaluationResponse {
  id: string;
  rfxEventId: string;
  supplierId: string;
  evaluatorId: string;
  technicalScore: number;
  commercialScore: number;
  comments: string | null;
  status: 'draft' | 'locked';
  lockedAtUtc: string | null;
}

export interface ClarificationResponse {
  id: string;
  rfxEventId: string;
  supplierId: string;
  category: string;
  question: string;
  response: string | null;
  isMaterialDeviation: boolean;
  status: 'pending' | 'resolved';
  createdAtUtc: string;
  resolvedAtUtc: string | null;
}
