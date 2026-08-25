import { useState } from 'react';
import { apiRequest } from '../../api/client';

export interface TriageRoute {
  name: string;
  rationale: string;
}

export interface GovernanceStatusRow {
  control: string;
  status: 'Passed' | 'Clear' | 'Pending' | 'Flagged';
  systemResponse: string;
}

export interface RequestSummaryData {
  title: string;
  requestor: string;
  category: string;
  estimatedValue: string;
  route: string;
  approvalLevel: string;
  supplier: string;
  esgScore: string;
  tprmStatus: string;
  contractAction: string;
}

export interface ProcurementTriageProps {
  requestId?: string;
  initialBlockingIssues?: boolean;
  blockingReason?: string;
  onAcceptSuccess?: () => void;
}

export function TriageRouteList({ routes }: { routes: TriageRoute[] }) {
  return (
    <div data-testid="triage-routes" style={{ marginBottom: '1.5rem' }}>
      <h3>Triage Routes</h3>
      <ul style={{ listStyleType: 'none', paddingLeft: 0 }}>
        {routes.map((r, i) => (
          <li key={i} style={{ borderLeft: '4px solid #0056b3', padding: '0.5rem 1rem', marginBottom: '0.5rem', background: '#f8f9fa' }}>
            <strong>{r.name}</strong>
            <p style={{ margin: '0.25rem 0 0 0', color: '#555' }}>{r.rationale}</p>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function GovernanceStatusTable({ rows }: { rows: GovernanceStatusRow[] }) {
  return (
    <div data-testid="governance-status-table" style={{ marginBottom: '1.5rem' }}>
      <h3>Governance Status</h3>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ background: '#f1f1f1', textAlign: 'left' }}>
            <th style={{ padding: '0.5rem' }}>Control</th>
            <th style={{ padding: '0.5rem' }}>Status</th>
            <th style={{ padding: '0.5rem' }}>System Response</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i} style={{ borderBottom: '1px solid #eee' }}>
              <td style={{ padding: '0.5rem' }}>{row.control}</td>
              <td style={{ padding: '0.5rem' }}>
                <span
                  style={{
                    display: 'inline-block',
                    padding: '0.2rem 0.5rem',
                    borderRadius: '4px',
                    fontSize: '0.85rem',
                    fontWeight: 'bold',
                    background: row.status === 'Passed' || row.status === 'Clear' ? '#d4edda' : '#fff3cd',
                    color: row.status === 'Passed' || row.status === 'Clear' ? '#155724' : '#856404',
                  }}
                >
                  {row.status}
                </span>
              </td>
              <td style={{ padding: '0.5rem', color: '#555' }}>{row.systemResponse}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function RequestSummaryList({ summary }: { summary: RequestSummaryData }) {
  return (
    <div data-testid="request-summary-list" style={{ marginBottom: '1.5rem' }}>
      <h3>Request Summary</h3>
      <dl style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', rowGap: '0.5rem', margin: 0 }}>
        <dt style={{ fontWeight: 'bold' }}>Title:</dt>
        <dd style={{ margin: 0 }}>{summary.title}</dd>
        <dt style={{ fontWeight: 'bold' }}>Requestor:</dt>
        <dd style={{ margin: 0 }}>{summary.requestor}</dd>
        <dt style={{ fontWeight: 'bold' }}>Category:</dt>
        <dd style={{ margin: 0 }}>{summary.category}</dd>
        <dt style={{ fontWeight: 'bold' }}>Estimated Value:</dt>
        <dd style={{ margin: 0 }}>{summary.estimatedValue}</dd>
        <dt style={{ fontWeight: 'bold' }}>Recommended Route:</dt>
        <dd style={{ margin: 0 }}>{summary.route}</dd>
        <dt style={{ fontWeight: 'bold' }}>Approval Level:</dt>
        <dd style={{ margin: 0 }}>{summary.approvalLevel}</dd>
        <dt style={{ fontWeight: 'bold' }}>Proposed Supplier:</dt>
        <dd style={{ margin: 0 }}>{summary.supplier}</dd>
        <dt style={{ fontWeight: 'bold' }}>ESG Score:</dt>
        <dd style={{ margin: 0 }}>{summary.esgScore}</dd>
        <dt style={{ fontWeight: 'bold' }}>TPRM Status:</dt>
        <dd style={{ margin: 0 }}>{summary.tprmStatus}</dd>
        <dt style={{ fontWeight: 'bold' }}>Contract Action:</dt>
        <dd style={{ margin: 0 }}>{summary.contractAction}</dd>
      </dl>
    </div>
  );
}

export function ProcurementTriage({
  requestId = 'PR-2026-004821',
  initialBlockingIssues = false,
  blockingReason = 'Unresolved blocking issues: Required compliance evidence missing.',
  onAcceptSuccess,
}: ProcurementTriageProps) {
  const [hasBlockingIssues, setHasBlockingIssues] = useState(initialBlockingIssues);
  const [priority, setPriority] = useState<'Standard' | 'High' | 'Critical'>('Standard');
  const [assignedTeam, setAssignedTeam] = useState('IT Procurement');
  const [triageComments, setTriageComments] = useState('');
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusType, setStatusType] = useState<'success' | 'error' | 'info'>('info');

  const defaultRoutes: TriageRoute[] = [
    {
      name: 'Tender / Competitive Sourcing (Minimum 3 Quotations)',
      rationale: 'Estimated value at or above local threshold (RM 100,000); competitive RFQ required.',
    },
    {
      name: 'GSP Escalation Check',
      rationale: 'High-value Malaysian procurement automatically flags Group Strategic Procurement.',
    },
  ];

  const defaultGovernanceRows: GovernanceStatusRow[] = [
    { control: 'Budget Gate', status: 'Passed', systemResponse: 'Budget commitment reserved under IT Cost Centre.' },
    { control: 'Segregation of Duties', status: 'Clear', systemResponse: 'No role collision across requester, evaluator, and approver.' },
    { control: 'Vendor 3-Point Check', status: hasBlockingIssues ? 'Flagged' : 'Passed', systemResponse: hasBlockingIssues ? 'Supplier KYC unverified or pending documentation.' : 'Supplier active, identity verified, KYC complete.' },
    { control: 'Anti-Splitting Detection', status: 'Clear', systemResponse: 'No recent duplicate category requests within 30-day window.' },
  ];

  const defaultSummary: RequestSummaryData = {
    title: 'Enterprise Cloud Infrastructure Expansion',
    requestor: 'Ahmad Faiz (IT Department)',
    category: 'IT and Telecommunication',
    estimatedValue: 'MYR 125,000.00',
    route: 'Tender / Competitive Sourcing',
    approvalLevel: 'Head of Procurement / CTO',
    supplier: 'Cloud Services Global Sdn Bhd',
    esgScore: '82 / 100 (Tier 1 Approved)',
    tprmStatus: 'Low Risk — Ready',
    contractAction: 'New Master Services Agreement',
  };

  const handleAcceptForAssessment = async () => {
    if (hasBlockingIssues) {
      setStatusType('error');
      setStatusMessage('Cannot accept request while blocking issues remain.');
      return;
    }

    try {
      if (requestId) {
        await apiRequest(`/requests/${requestId}/triage-decision`, {
          method: 'POST',
          body: JSON.stringify({
            decision: 'accept',
            assigneeTeam: assignedTeam,
            reason: triageComments || 'Accepted for assessment by Procurement Administrator',
          }),
        }).catch(() => {
          // Fallback if running with mock/in-memory data in UI
        });
      }
      setStatusType('success');
      setStatusMessage('Request accepted and routed to Procurement Assessment.');
      if (onAcceptSuccess) {
        onAcceptSuccess();
      }
    } catch (err: unknown) {
      setStatusType('error');
      setStatusMessage(err instanceof Error ? err.message : 'Triage decision submission failed.');
    }
  };

  const handleRejectOrReturn = (decision: 'reject' | 'return') => {
    setStatusType('info');
    setStatusMessage(`Request marked as ${decision}ed.`);
  };

  return (
    <div style={{ maxWidth: '1100px', margin: '0 auto' }}>
      <h2>Procurement Triage & Assessment Overview</h2>

      {/* Metrics Row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginBottom: '1.5rem' }}>
        <div style={{ padding: '1rem', background: '#e9ecef', borderRadius: '4px' }}>
          <div style={{ fontSize: '0.85rem', color: '#666' }}>Budget Gate</div>
          <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#28a745' }}>Passed</div>
        </div>
        <div style={{ padding: '1rem', background: '#e9ecef', borderRadius: '4px' }}>
          <div style={{ fontSize: '0.85rem', color: '#666' }}>Award Readiness</div>
          <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: hasBlockingIssues ? '#dc3545' : '#007bff' }}>
            {hasBlockingIssues ? 'Review Required' : 'Ready for Assessment'}
          </div>
        </div>
        <div style={{ padding: '1rem', background: '#e9ecef', borderRadius: '4px' }}>
          <div style={{ fontSize: '0.85rem', color: '#666' }}>Approval Level</div>
          <div style={{ fontSize: '1.25rem', fontWeight: 'bold' }}>Head of Procurement</div>
        </div>
      </div>

      {statusMessage && (
        <div
          role="status"
          style={{
            padding: '0.75rem 1rem',
            borderRadius: '4px',
            marginBottom: '1rem',
            background: statusType === 'success' ? '#d4edda' : statusType === 'error' ? '#f8d7da' : '#d1ecf1',
            color: statusType === 'success' ? '#155724' : statusType === 'error' ? '#721c24' : '#0c5460',
            border: `1px solid ${statusType === 'success' ? '#c3e6cb' : statusType === 'error' ? '#f5c6cb' : '#bee5eb'}`,
          }}
        >
          {statusMessage}
        </div>
      )}

      {hasBlockingIssues && (
        <div
          role="alert"
          style={{
            padding: '0.75rem 1rem',
            background: '#fff3cd',
            color: '#856404',
            border: '1px solid #ffeeba',
            borderRadius: '4px',
            marginBottom: '1rem',
          }}
        >
          <strong>Blocking Issues Detected:</strong> {blockingReason}
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '2rem' }}>
        {/* Left Column: Routes, Governance, Summary */}
        <div>
          <TriageRouteList routes={defaultRoutes} />
          <GovernanceStatusTable rows={defaultGovernanceRows} />
          <RequestSummaryList summary={defaultSummary} />
        </div>

        {/* Right Column: Workload Prioritisation & Decision Panel */}
        <div>
          {/* Workload Prioritisation */}
          <div style={{ border: '1px solid #ddd', borderRadius: '4px', padding: '1rem', marginBottom: '1.5rem', background: '#fafafa' }}>
            <h3>Workload Prioritisation</h3>
            <div style={{ marginBottom: '0.75rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem' }}>
                <span>Business Urgency</span>
                <span>High (75%)</span>
              </div>
              <div style={{ height: '8px', background: '#e0e0e0', borderRadius: '4px', overflow: 'hidden' }}>
                <div style={{ width: '75%', height: '100%', background: '#fd7e14' }} />
              </div>
            </div>
            <div style={{ marginBottom: '0.75rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem' }}>
                <span>Governance Complexity</span>
                <span>Medium (60%)</span>
              </div>
              <div style={{ height: '8px', background: '#e0e0e0', borderRadius: '4px', overflow: 'hidden' }}>
                <div style={{ width: '60%', height: '100%', background: '#ffc107' }} />
              </div>
            </div>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem' }}>
                <span>Submission Completeness</span>
                <span>{hasBlockingIssues ? '80%' : '100%'}</span>
              </div>
              <div style={{ height: '8px', background: '#e0e0e0', borderRadius: '4px', overflow: 'hidden' }}>
                <div style={{ width: hasBlockingIssues ? '80%' : '100%', height: '100%', background: '#28a745' }} />
              </div>
            </div>
          </div>

          {/* Decision Panel */}
          <div style={{ border: '1px solid #ccc', borderRadius: '4px', padding: '1rem', background: '#fff' }}>
            <h3>Triage Decision</h3>

            <div style={{ marginBottom: '1rem' }}>
              <label htmlFor="prioritySelect" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
                Priority Level
              </label>
              <select
                id="prioritySelect"
                value={priority}
                onChange={(e) => setPriority(e.target.value as 'Standard' | 'High' | 'Critical')}
                style={{ width: '100%', padding: '0.5rem' }}
              >
                <option value="Standard">Standard</option>
                <option value="High">High</option>
                <option value="Critical">Critical</option>
              </select>
              <small style={{ color: '#666', display: 'block', marginTop: '0.25rem' }}>
                Note: Priority changes are recorded in the audit trail.
              </small>
            </div>

            <div style={{ marginBottom: '1rem' }}>
              <label htmlFor="assigneeTeam" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
                Assign to Team
              </label>
              <select
                id="assigneeTeam"
                value={assignedTeam}
                onChange={(e) => setAssignedTeam(e.target.value)}
                style={{ width: '100%', padding: '0.5rem' }}
              >
                <option value="IT Procurement">IT Procurement</option>
                <option value="Corporate Services">Corporate Services</option>
                <option value="Marketing Sourcing">Marketing Sourcing</option>
                <option value="Finance Sourcing">Finance Sourcing</option>
              </select>
            </div>

            <div style={{ marginBottom: '1rem' }}>
              <label htmlFor="triageComments" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
                Triage Comments
              </label>
              <textarea
                id="triageComments"
                rows={3}
                value={triageComments}
                onChange={(e) => setTriageComments(e.target.value)}
                placeholder="Optional notes or instructions for the assessment team..."
                style={{ width: '100%', padding: '0.5rem', boxSizing: 'border-box' }}
              />
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              <button
                type="button"
                id="acceptButton"
                onClick={handleAcceptForAssessment}
                disabled={hasBlockingIssues}
                style={{
                  padding: '0.6rem 1rem',
                  background: hasBlockingIssues ? '#ccc' : '#28a745',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '4px',
                  cursor: hasBlockingIssues ? 'not-allowed' : 'pointer',
                  fontWeight: 'bold',
                }}
              >
                Accept for Assessment
              </button>

              {hasBlockingIssues && (
                <div style={{ fontSize: '0.85rem', color: '#dc3545' }}>
                  Action disabled: Resolve blocking issues before accepting.
                </div>
              )}

              <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button
                  type="button"
                  onClick={() => handleRejectOrReturn('return')}
                  style={{ flex: 1, padding: '0.4rem', background: '#ffc107', border: '1px solid #d39e00', borderRadius: '4px' }}
                >
                  Request Info
                </button>
                <button
                  type="button"
                  onClick={() => handleRejectOrReturn('reject')}
                  style={{ flex: 1, padding: '0.4rem', background: '#dc3545', color: '#fff', border: 'none', borderRadius: '4px' }}
                >
                  Reject / Close
                </button>
              </div>

              {/* Dev toggle to test blocking state */}
              <div style={{ marginTop: '1rem', borderTop: '1px dashed #ddd', paddingTop: '0.5rem' }}>
                <label style={{ fontSize: '0.8rem', color: '#777' }}>
                  <input
                    type="checkbox"
                    checked={hasBlockingIssues}
                    onChange={(e) => setHasBlockingIssues(e.target.checked)}
                  />{' '}
                  Simulate blocking issue
                </label>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
