import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';
import type { PagedResponse } from '../../types/requests';
import { PageHeader, Card, StatusMessage, Badge, inputClass, labelClass, tableClass, theadClass, thClass, thRightClass, trClass, tdClass, tdRightClass, TableWrap, ButtonPrimary } from '../../components/ui';

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
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
      <PageHeader title="Finance Commitment Console — Requisition Finalisation" />

      {actionMessage && <StatusMessage tone="success">{actionMessage}</StatusMessage>}

      <Card>
        <div className="flex flex-wrap items-end gap-3 mb-4">
          <div className="flex-1 min-w-[200px] max-w-sm">
            <label htmlFor="searchFinalised" className={labelClass}>
              Search
            </label>
            <input
              id="searchFinalised"
              type="text"
              className={inputClass}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Reference, title, or hire name..."
            />
          </div>
        </div>

        {data && (
          <TableWrap>
            <table className={tableClass}>
              <thead className={theadClass}>
                <tr className={trClass}>
                  <th className={thClass}>Reference</th>
                  <th className={thClass}>Title</th>
                  <th className={thRightClass}>Estimated Cost</th>
                  <th className={thClass}>Final Spent</th>
                  <th className={thClass}>Status</th>
                  <th className={thClass}>Action</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((r) => (
                  <tr key={r.id} className={trClass}>
                    <td className={`${tdClass} font-mono text-2xs text-accent font-medium`}>{r.reference}</td>
                    <td className={tdClass}>{r.title}</td>
                    <td className={tdRightClass}>MYR {r.estCost.toLocaleString()}</td>
                    <td className={tdRightClass}>{r.finalSpent ? `MYR ${r.finalSpent.toLocaleString()}` : '—'}</td>
                    <td className={tdClass}>
                      <Badge tone={r.status === 'AwaitingFinalisation' ? 'warning' : 'neutral'}>{r.status}</Badge>
                    </td>
                    <td className={tdClass}>
                      {r.status === 'AwaitingFinalisation' && (
                        <ButtonPrimary onClick={() => handleFinalise(r.id, r.estCost)}>Finalise</ButtonPrimary>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableWrap>
        )}
      </Card>
    </div>
  );
}
