import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';
import type { DashboardSummaryResponse } from '../../types/requests';

interface Props {
  onNavigateToIntake: () => void;
  onNavigateToRequisitions: () => void;
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

  return (
    <div>
      <h2>Workspace Dashboard</h2>

      <section aria-label="Quick Actions">
        <button type="button" onClick={onNavigateToIntake}>New Requisition</button>
        <button type="button" onClick={onNavigateToRequisitions}>My Requisitions</button>
      </section>

      {summary && (
        <section aria-label="Budget Health" style={{ marginTop: '1.5rem' }}>
          <h3>Budget Health</h3>
          <p>Allocated: MYR {summary.budgetHealth.allocated.toLocaleString()}</p>
          <p>Available: MYR {summary.budgetHealth.available.toLocaleString()}</p>
        </section>
      )}
    </div>
  );
}
