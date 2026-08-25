import { useState } from 'react';
import { apiRequest } from '../../api/client';
import { PageHeader, Card, StatusMessage, inputClass, labelClass, tableClass, theadClass, thClass, trClass, tdClass, TableWrap, ButtonPrimary, ButtonGhost } from '../../components/ui';

interface ApprovalTask {
  id: string;
  requestId: string;
  gate: string;
  approverId: string;
  status: string;
  decisionComments?: string;
  decidedAtUtc?: string;
}

interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export function ApprovalInbox() {
  const [data, setData] = useState<PagedResponse<ApprovalTask> | null>(null);
  const [status, setStatus] = useState('Pending');
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  const loadData = () => {
    apiRequest<PagedResponse<ApprovalTask>>(`/approvals?status=${status}`)
      .then(setData)
      .catch(() => {});
  };

  useState(() => {
    loadData();
  });

  const handleDecision = async (taskId: string, decision: 'approve' | 'reject') => {
    setActionMessage(null);
    try {
      await apiRequest(`/approvals/${taskId}/decision`, {
        method: 'POST',
        body: JSON.stringify({ decision, comments: 'Actioned via inbox' }),
      });
      setActionMessage(`Task successfully ${decision}d.`);
      loadData();
    } catch (err: unknown) {
      setActionMessage(err instanceof Error ? err.message : 'Decision failed');
    }
  };

  return (
    <div>
      <PageHeader title="Workspace — Approval Inbox" />

      {actionMessage && (
        <StatusMessage tone="success">{actionMessage}</StatusMessage>
      )}

      <Card>
        <div className="flex flex-wrap items-end gap-3 mb-4">
          <div className="w-48">
            <label htmlFor="statusFilter" className={labelClass}>
              Status
            </label>
            <select
              id="statusFilter"
              className={inputClass}
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setTimeout(loadData, 0);
              }}
            >
              <option value="Pending">Pending Approval</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
            </select>
          </div>
        </div>

        {data && (
          <TableWrap>
            <table className={tableClass}>
              <thead className={theadClass}>
                <tr className={trClass}>
                  <th className={thClass}>Request ID</th>
                  <th className={thClass}>Gate</th>
                  <th className={thClass}>Approver</th>
                  <th className={thClass}>Status</th>
                  <th className={thClass}>Action</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((task) => (
                  <tr key={task.id} className={trClass}>
                    <td className={`${tdClass} font-mono text-2xs text-accent font-medium`}>{task.requestId}</td>
                    <td className={tdClass}>{task.gate}</td>
                    <td className={tdClass}>{task.approverId}</td>
                    <td className={tdClass}>{task.status}</td>
                    <td className={tdClass}>
                      {task.status === 'Pending' && (
                        <div className="flex items-center gap-2">
                          <ButtonPrimary onClick={() => handleDecision(task.id, 'approve')}>Approve</ButtonPrimary>
                          <ButtonGhost onClick={() => handleDecision(task.id, 'reject')}>Reject</ButtonGhost>
                        </div>
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
