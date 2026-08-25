import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';
import type { PagedResponse } from '../../types/requests';

interface RequisitionItem {
  id: string;
  reference: string;
  title: string;
  status: string;
  estCost: number;
  finalSpent?: number;
  finalisedOn?: string;
  notes?: string;
}

export function RequisitionFinalisation() {
  const [data, setData] = useState<PagedResponse<RequisitionItem> | null>(null);
  const [search, setSearch] = useState('');
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  const loadData = () => {
    const params = new URLSearchParams({ page: '1', pageSize: '20' });
    if (search) params.set('search', search);
    apiRequest<PagedResponse<RequisitionItem>>(`/requisitions?${params.toString()}`)
      .then(setData)
      .catch(() => {});
  };

  useEffect(() => {
    loadData();
  }, [search]);

  const handleFinalise = async (id: string, estCost: number) => {
    try {
      await apiRequest(`/requisitions/${id}/finalise`, {
        method: 'POST',
        body: JSON.stringify({ finalSpent: estCost, notes: 'Finalised within estimate' }),
      });
      setActionMessage('Requisition finalised successfully.');
      loadData();
    } catch (err: unknown) {
      setActionMessage(err instanceof Error ? err.message : 'Action failed');
    }
  };

  return (
    <div>
      <h2>Finance Commitment Console — Requisition Finalisation</h2>

      {actionMessage && <div role="status" style={{ color: 'green', marginBottom: '1rem' }}>{actionMessage}</div>}

      <div>
        <label htmlFor="searchFinalised">Search</label>
        <input
          id="searchFinalised"
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Reference, title, or hire name..."
        />
      </div>

      {data && (
        <table style={{ marginTop: '1rem' }}>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Title</th>
              <th>Estimated Cost</th>
              <th>Final Spent</th>
              <th>Status</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((r) => (
              <tr key={r.id}>
                <td>{r.reference}</td>
                <td>{r.title}</td>
                <td>MYR {r.estCost.toLocaleString()}</td>
                <td>{r.finalSpent ? `MYR ${r.finalSpent.toLocaleString()}` : '—'}</td>
                <td>{r.status}</td>
                <td>
                  {r.status === 'AwaitingFinalisation' && (
                    <button type="button" onClick={() => handleFinalise(r.id, r.estCost)}>
                      Finalise
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
