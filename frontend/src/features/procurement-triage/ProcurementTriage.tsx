import { useState } from 'react';
import { apiRequest } from '../../api/client';
import { Card, Badge, PageHeader, StatusMessage, inputClass, labelClass, tableClass, theadClass, thClass, trClass, tdClass, TableWrap, ButtonPrimary, ButtonGhost, ButtonDanger } from '../../components/ui';

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
    <div data-testid="triage-routes" className="mb-5">
      <h3 className="text-[0.9375rem] font-semibold text-ink mb-2">Triage Routes</h3>
      <ul className="space-y-2">
        {routes.map((r, i) => (
          <li key={i} className="border-l-4 border-accent px-4 py-2.5 bg-canvas-subtle rounded-r-md">
            <strong className="text-sm text-ink">{r.name}</strong>
            <p className="mt-1 text-2xs text-ink-muted">{r.rationale}</p>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function GovernanceStatusTable({ rows }: { rows: GovernanceStatusRow[] }) {
  return (
    <div data-testid="governance-status-table" className="mb-5">
      <h3 className="text-[0.9375rem] font-semibold text-ink mb-2">Governance Status</h3>
      <TableWrap>
        <table className={tableClass}>
          <thead className={theadClass}>
            <tr className={trClass}>
              <th className={thClass}>Control</th>
              <th className={thClass}>Status</th>
              <th className={thClass}>System Response</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr key={i} className={trClass}>
                <td className={tdClass}>{row.control}</td>
                <td className={tdClass}>
                  <Badge tone={row.status === 'Passed' || row.status === 'Clear' ? 'success' : 'warning'}>
                    {row.status}
                  </Badge>
                </td>
                <td className={`${tdClass} text-ink-muted`}>{row.systemResponse}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableWrap>
    </div>
  );
}

export function RequestSummaryList({ summary }: { summary: RequestSummaryData }) {
  const rows: Array<[string, string]> = [
    ['Title', summary.title],
    ['Requestor', summary.requestor],
    ['Category', summary.category],
    ['Estimated Value', summary.estimatedValue],
    ['Recommended Route', summary.route],
    ['Approval Level', summary.approvalLevel],
    ['Proposed Supplier', summary.supplier],
    ['ESG Score', summary.esgScore],
    ['TPRM Status', summary.tprmStatus],
    ['Contract Action', summary.contractAction],
  ];
  return (
    <div data-testid="request-summary-list">
      <h3 className="text-[0.9375rem] font-semibold text-ink mb-2">Request Summary</h3>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-ink-muted">{label}:</dt>
            <dd className="m-0 text-ink font-medium">{value}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

function ProgressRow({ label, value, pct, color }: { label: string; value: string; pct: number; color: string }) {
  return (
    <div className="mb-3">
      <div className="flex justify-between text-2xs text-ink-muted mb-1">
        <span>{label}</span>
        <span>{value}</span>
      </div>
      <div className="h-2 bg-canvas-subtle rounded-full overflow-hidden">
        <div className="h-full rounded-full" style={{ width: `${pct}%`, background: color }} />
      </div>
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
    <div>
      <PageHeader title="Procurement Triage & Assessment Overview" />

      <div className="grid gap-3 grid-cols-1 sm:grid-cols-3 mb-5">
        <div className="bg-surface border border-line rounded-md px-4 py-3">
          <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted">Budget Gate</div>
          <div className="text-xl font-bold leading-tight mt-1 text-success">Passed</div>
        </div>
        <div className="bg-surface border border-line rounded-md px-4 py-3">
          <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted">Award Readiness</div>
          <div className={`text-xl font-bold leading-tight mt-1 ${hasBlockingIssues ? 'text-danger' : 'text-info'}`}>
            {hasBlockingIssues ? 'Review Required' : 'Ready for Assessment'}
          </div>
        </div>
        <div className="bg-surface border border-line rounded-md px-4 py-3">
          <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted">Approval Level</div>
          <div className="text-xl font-bold leading-tight mt-1 text-ink">Head of Procurement</div>
        </div>
      </div>

      {statusMessage && (
        <div role="status">
          <StatusMessage tone={statusType === 'success' ? 'success' : statusType === 'error' ? 'danger' : 'info'}>
            {statusMessage}
          </StatusMessage>
        </div>
      )}

      {hasBlockingIssues && (
        <div role="alert">
          <StatusMessage tone="warning">
            <strong>Blocking Issues Detected:</strong> {blockingReason}
          </StatusMessage>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-5">
        <div className="space-y-5">
          <Card>
            <TriageRouteList routes={defaultRoutes} />
            <GovernanceStatusTable rows={defaultGovernanceRows} />
            <RequestSummaryList summary={defaultSummary} />
          </Card>
        </div>

        <div className="space-y-5">
          <Card title="Workload Prioritisation">
            <ProgressRow label="Business Urgency" value="High (75%)" pct={75} color="#F59E0B" />
            <ProgressRow label="Governance Complexity" value="Medium (60%)" pct={60} color="#F59E0B" />
            <ProgressRow
              label="Submission Completeness"
              value={hasBlockingIssues ? '80%' : '100%'}
              pct={hasBlockingIssues ? 80 : 100}
              color="#16A34A"
            />
          </Card>

          <Card title="Triage Decision">
            <div className="mb-3">
              <label htmlFor="prioritySelect" className={labelClass}>
                Priority Level
              </label>
              <select
                id="prioritySelect"
                className={inputClass}
                value={priority}
                onChange={(e) => setPriority(e.target.value as 'Standard' | 'High' | 'Critical')}
              >
                <option value="Standard">Standard</option>
                <option value="High">High</option>
                <option value="Critical">Critical</option>
              </select>
              <p className="text-2xs text-ink-muted mt-1">Note: Priority changes are recorded in the audit trail.</p>
            </div>

            <div className="mb-3">
              <label htmlFor="assigneeTeam" className={labelClass}>
                Assign to Team
              </label>
              <select id="assigneeTeam" className={inputClass} value={assignedTeam} onChange={(e) => setAssignedTeam(e.target.value)}>
                <option value="IT Procurement">IT Procurement</option>
                <option value="Corporate Services">Corporate Services</option>
                <option value="Marketing Sourcing">Marketing Sourcing</option>
                <option value="Finance Sourcing">Finance Sourcing</option>
              </select>
            </div>

            <div className="mb-4">
              <label htmlFor="triageComments" className={labelClass}>
                Triage Comments
              </label>
              <textarea
                id="triageComments"
                rows={3}
                className={inputClass}
                value={triageComments}
                onChange={(e) => setTriageComments(e.target.value)}
                placeholder="Optional notes or instructions for the assessment team..."
              />
            </div>

            <div className="space-y-2">
              <ButtonPrimary id="acceptButton" onClick={handleAcceptForAssessment} disabled={hasBlockingIssues} className="w-full py-2">
                Accept for Assessment
              </ButtonPrimary>

              {hasBlockingIssues && (
                <p className="text-2xs text-danger">Action disabled: Resolve blocking issues before accepting.</p>
              )}

              <div className="flex gap-2 pt-1">
                <ButtonGhost className="flex-1" onClick={() => handleRejectOrReturn('return')}>
                  Request Info
                </ButtonGhost>
                <ButtonDanger className="flex-1" onClick={() => handleRejectOrReturn('reject')}>
                  Reject / Close
                </ButtonDanger>
              </div>

              <div className="mt-3 pt-2 border-t border-dashed border-line">
                <label className="text-2xs text-ink-muted inline-flex items-center gap-1.5 cursor-pointer">
                  <input type="checkbox" checked={hasBlockingIssues} onChange={(e) => setHasBlockingIssues(e.target.checked)} />
                  Simulate blocking issue
                </label>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}
