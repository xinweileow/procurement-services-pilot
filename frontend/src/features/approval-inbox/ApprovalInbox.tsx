import { useState } from 'react';
import { apiRequest } from '../../api/client';

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
      <h2>Workspace — Approval Inbox</h2>

      {actionMessage && <div role="status" style={{ color: 'green', marginBottom: '1rem' }}>{actionMessage}</div>}

      <div>
        <label htmlFor="statusFilter">Status</label>
        <select
          id="statusFilter"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value);
            // Re-load trigger
            setTimeout(loadData, 0);
          }}
        >
          <option value="Pending">Pending Approval</option>
          <option value="Approved">Approved</option>
          <option value="Rejected">Rejected</option>
        </select>
      </div>

      {data && (
        <table style={{ marginTop: '1rem' }}>
          <thead>
            <tr>
              <th>Request ID</th>
              <th>Gate</th>
              <th>Approver</th>
              <th>Status</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((task) => (
              <tr key={task.id}>
                <td>{task.requestId}</td>
                <td>{task.gate}</td>
                <td>{task.approverId}</td>
                <td>{task.status}</td>
                <td>
                  {task.status === 'Pending' && (
                    <>
                      <button type="button" onClick={() => handleDecision(task.id, 'approve')}>
                        Approve
                      </button>{' '}
                      <button type="button" onClick={() => handleDecision(task.id, 'reject')}>
                        Reject
                      </button>
                    </>
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
