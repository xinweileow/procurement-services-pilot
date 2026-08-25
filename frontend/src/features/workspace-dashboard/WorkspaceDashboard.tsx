import { useEffect, useState } from 'react';
import { FilePlus, Inbox, CircleCheck, DollarSign, Activity } from 'lucide-react';
import { apiRequest } from '../../api/client';
import type { DashboardSummaryResponse } from '../../types/requests';

interface Props {
  onNavigateToIntake: () => void;
  onNavigateToRequisitions: () => void;
}

function formatMyr(value: number): string {
  return `MYR ${value.toLocaleString()}`;
}

export function WorkspaceDashboard({ onNavigateToIntake, onNavigateToRequisitions }: Props) {
  const [summary, setSummary] = useState<DashboardSummaryResponse | null>(null);

  useEffect(() => {
    apiRequest<DashboardSummaryResponse>('/dashboard/summary')
      .then(setSummary)
      .catch(() => {
        // Silently fall back to empty summary if backend is not reachable in testing/offline mode
      });
  }, []);

  const health = summary?.budgetHealth;
  const utilisationPct = health && health.allocated > 0 ? ((health.reserved + health.spent) / health.allocated) * 100 : 0;

  return (
    <div>
      <div className="flex items-start justify-between gap-4 flex-wrap mb-5">
        <div className="min-w-0">
          <h1 className="text-[1.375rem] font-bold leading-tight tracking-tight text-ink">
            Workspace Dashboard
          </h1>
          <p className="text-[0.8125rem] text-ink-muted mt-1">Welcome back</p>
        </div>
      </div>

      <div className="grid gap-2 grid-cols-1 sm:grid-cols-2 lg:grid-cols-4" aria-label="Quick Actions">
        <button
          type="button"
          onClick={onNavigateToIntake}
          className="group flex items-center gap-2.5 px-4 py-3 bg-surface border border-line rounded-md text-ink transition-all hover:border-info hover:shadow-sm text-left"
        >
          <span className="w-9 h-9 rounded-md bg-info-soft text-info flex items-center justify-center flex-shrink-0">
            <FilePlus size={18} />
          </span>
          <span className="min-w-0">
            <span className="block font-semibold text-sm truncate group-hover:text-info">New Requisition</span>
            <span className="block text-2xs text-ink-muted truncate">Submit a new request</span>
          </span>
        </button>
        <button
          type="button"
          onClick={onNavigateToRequisitions}
          className="group flex items-center gap-2.5 px-4 py-3 bg-surface border border-line rounded-md text-ink transition-all hover:border-info hover:shadow-sm text-left"
        >
          <span className="w-9 h-9 rounded-md bg-info-soft text-info flex items-center justify-center flex-shrink-0">
            <Inbox size={18} />
          </span>
          <span className="min-w-0">
            <span className="block font-semibold text-sm truncate group-hover:text-info">My Requisitions</span>
            <span className="block text-2xs text-ink-muted truncate">View &amp; manage drafts</span>
          </span>
        </button>
        <div className="flex items-center gap-2.5 px-4 py-3 bg-surface border border-line rounded-md text-ink opacity-60">
          <span className="w-9 h-9 rounded-md bg-info-soft text-info flex items-center justify-center flex-shrink-0">
            <CircleCheck size={18} />
          </span>
          <span className="min-w-0">
            <span className="block font-semibold text-sm truncate">Approvals Inbox</span>
            <span className="block text-2xs text-ink-muted truncate">Pending your action</span>
          </span>
        </div>
        <div className="flex items-center gap-2.5 px-4 py-3 bg-surface border border-line rounded-md text-ink opacity-60">
          <span className="w-9 h-9 rounded-md bg-info-soft text-info flex items-center justify-center flex-shrink-0">
            <DollarSign size={18} />
          </span>
          <span className="min-w-0">
            <span className="block font-semibold text-sm truncate">Budget Console</span>
            <span className="block text-2xs text-ink-muted truncate">Pools, allocations, spend</span>
          </span>
        </div>
      </div>

      {health && (
        <section
          aria-label="Budget Health"
          className="bg-surface border border-line rounded-lg shadow-xs hover:shadow-sm transition-shadow"
        >
          <div className="flex items-center justify-between gap-3 px-[1.15rem] py-[0.9rem] border-b border-line text-[0.9375rem] font-semibold">
            <span className="inline-flex items-center gap-1.5 min-w-0 truncate">
              <Activity size={14} />
              Budget Health
            </span>
          </div>
          <div className="p-[1.15rem] space-y-3">
            <div>
              <div className="flex items-end justify-between mb-1">
                <span className="text-3xl font-bold text-ink tabular-nums">{utilisationPct.toFixed(2)}%</span>
                <span className="text-2xs text-ink-muted uppercase tracking-wider">Utilised</span>
              </div>
              <div className="h-2.5 bg-canvas-subtle rounded-full overflow-hidden">
                <div
                  className="h-full rounded-full bg-success transition-all"
                  style={{ width: `${Math.min(utilisationPct, 100)}%` }}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="bg-surface border border-line rounded-md px-4 py-3">
                <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted">Allocated</div>
                <div className="text-xl font-bold leading-tight mt-1 tabular-nums">{formatMyr(health.allocated)}</div>
              </div>
              <div className="bg-surface border border-line rounded-md px-4 py-3">
                <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted">Available</div>
                <div className="text-xl font-bold leading-tight mt-1 tabular-nums">{formatMyr(health.available)}</div>
              </div>
              <div className="bg-surface border border-line rounded-md px-4 py-3">
                <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted">Reserved</div>
                <div className="text-xl font-bold leading-tight mt-1 tabular-nums">{formatMyr(health.reserved)}</div>
                <div className="text-2xs text-ink-muted mt-1">Held until finalisation</div>
              </div>
              <div className="bg-surface border border-line rounded-md px-4 py-3">
                <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted">Spent</div>
                <div className="text-xl font-bold leading-tight mt-1 tabular-nums">{formatMyr(health.spent)}</div>
                <div className="text-2xs text-ink-muted mt-1">Invoiced</div>
              </div>
            </div>
          </div>
        </section>
      )}
    </div>
  );
}
