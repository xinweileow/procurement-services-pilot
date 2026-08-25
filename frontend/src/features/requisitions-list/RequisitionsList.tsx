import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';
import type { PagedResponse, RequestResponse } from '../../types/requests';
import { PageHeader, Card, inputClass, labelClass, tableClass, theadClass, thClass, thRightClass, trClass, tdClass, tdRightClass, TableWrap, ButtonGhost } from '../../components/ui';

export function RequisitionsList() {
  const [status, setStatus] = useState<string>('');
  const [page, setPage] = useState<number>(1);
  const [data, setData] = useState<PagedResponse<RequestResponse> | null>(null);

  useEffect(() => {
    const params = new URLSearchParams({ page: page.toString(), pageSize: '20' });
    if (status) params.set('status', status);
    apiRequest<PagedResponse<RequestResponse>>(`/requests?${params.toString()}`)
      .then(setData)
      .catch(() => {
        // Silently fall back to empty list if backend is not reachable in testing/offline mode
      });
  }, [status, page]);

  return (
    <div>
      <PageHeader title="Requisitions" />

      <Card>
        <div className="flex flex-wrap items-end gap-3 mb-4">
          <div className="w-48">
            <label htmlFor="statusFilter" className={labelClass}>
              Status Filter
            </label>
            <select
              id="statusFilter"
              className={inputClass}
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All</option>
              <option value="Draft">Draft</option>
              <option value="Submitted">Submitted</option>
              <option value="Approved">Approved</option>
            </select>
          </div>
        </div>

        {data && (
          <>
            <TableWrap>
              <table className={tableClass}>
                <thead className={theadClass}>
                  <tr className={trClass}>
                    <th className={thClass}>Request ID</th>
                    <th className={thClass}>Title</th>
                    <th className={thClass}>Category</th>
                    <th className={thRightClass}>Estimated Value</th>
                    <th className={thClass}>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((r) => (
                    <tr key={r.id} className={trClass}>
                      <td className={`${tdClass} font-mono text-2xs text-accent font-medium`}>{r.requestId}</td>
                      <td className={tdClass}>{r.title}</td>
                      <td className={tdClass}>{r.category}</td>
                      <td className={tdRightClass}>
                        {r.currency} {r.estimatedValue.toLocaleString()}
                      </td>
                      <td className={tdClass}>{r.status}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </TableWrap>

            <div className="mt-3 flex items-center justify-between text-2xs text-ink-muted">
              <span>
                Showing {data.items.length} of {data.total} entries
              </span>
              <div className="flex items-center gap-2">
                <ButtonGhost disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                  Previous
                </ButtonGhost>
                <ButtonGhost disabled={page * 20 >= data.total} onClick={() => setPage((p) => p + 1)}>
                  Next
                </ButtonGhost>
              </div>
            </div>
          </>
        )}
      </Card>
    </div>
  );
}
