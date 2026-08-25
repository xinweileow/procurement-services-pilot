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
