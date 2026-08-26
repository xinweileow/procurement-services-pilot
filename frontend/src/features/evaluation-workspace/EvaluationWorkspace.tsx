import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';
import type { ClarificationResponse, EvaluationResponse, PagedResponse, RfxEventResponse } from '../../types/requests';
import {
  PageHeader,
  Card,
  StatusMessage,
  Badge,
  inputClass,
  labelClass,
  tableClass,
  theadClass,
  thClass,
  trClass,
  tdClass,
  TableWrap,
  ButtonPrimary,
  ButtonGhost,
} from '../../components/ui';

export function EvaluationWorkspace() {
  const [events, setEvents] = useState<RfxEventResponse[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [evaluations, setEvaluations] = useState<EvaluationResponse[]>([]);
  const [clarifications, setClarifications] = useState<ClarificationResponse[]>([]);
  const [message, setMessage] = useState<{ tone: 'success' | 'danger'; text: string } | null>(null);

  const [supplierId, setSupplierId] = useState('');
  const [evaluatorId, setEvaluatorId] = useState('');
  const [technicalScore, setTechnicalScore] = useState<number>(0);
  const [commercialScore, setCommercialScore] = useState<number>(0);
  const [comments, setComments] = useState('');
  const [lockOnSave, setLockOnSave] = useState(false);

  const [clarSupplierId, setClarSupplierId] = useState('');
  const [clarCategory, setClarCategory] = useState('Technical');
  const [clarQuestion, setClarQuestion] = useState('');
  const [clarMaterial, setClarMaterial] = useState(false);
  const [resolveResponses, setResolveResponses] = useState<Record<string, string>>({});

  const [gateSupplierId, setGateSupplierId] = useState('');
  const [gateDecision, setGateDecision] = useState<'proceed' | 'return' | 'reject'>('proceed');

  useEffect(() => {
    apiRequest<PagedResponse<RfxEventResponse>>('/rfx-events')
      .then((res) => setEvents(res.items))
      .catch(() => {});
  }, []);

  const loadEvaluations = (id: string) => {
    apiRequest<EvaluationResponse[]>(`/rfx-events/${id}/evaluations`)
      .then(setEvaluations)
      .catch(() => {});
  };

  const loadClarifications = (id: string) => {
    apiRequest<ClarificationResponse[]>(`/rfx-events/${id}/clarifications`)
      .then(setClarifications)
      .catch(() => {});
  };

  useEffect(() => {
    if (selectedId) {
      loadEvaluations(selectedId);
      loadClarifications(selectedId);
    } else {
      setEvaluations([]);
      setClarifications([]);
    }
  }, [selectedId]);

  const handleScore = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedId || !supplierId.trim() || !evaluatorId.trim()) {
      setMessage({ tone: 'danger', text: 'Select an RFx event and provide both Supplier ID and Evaluator ID.' });
      return;
    }
    try {
      await apiRequest(`/rfx-events/${selectedId}/evaluations`, {
        method: 'POST',
        body: JSON.stringify({
          supplierId,
          evaluatorId,
          technicalScore,
          commercialScore,
          comments: comments || undefined,
          lock: lockOnSave,
        }),
      });
      setMessage({ tone: 'success', text: lockOnSave ? 'Evaluation scored and locked.' : 'Evaluation scored.' });
      loadEvaluations(selectedId);
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Evaluation already locked — cannot be modified.' });
    }
  };

  const handleRaiseClarification = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedId || !clarSupplierId.trim() || !clarQuestion.trim()) {
      setMessage({ tone: 'danger', text: 'Select an RFx event and provide both Supplier ID and a question.' });
      return;
    }
    try {
      await apiRequest(`/rfx-events/${selectedId}/clarifications`, {
        method: 'POST',
        body: JSON.stringify({ supplierId: clarSupplierId, category: clarCategory, question: clarQuestion, isMaterialDeviation: clarMaterial }),
      });
      setMessage({ tone: 'success', text: 'Clarification raised.' });
      setClarQuestion('');
      loadClarifications(selectedId);
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Failed to raise clarification.' });
    }
  };

  const handleResolve = async (clarificationId: string) => {
    const response = resolveResponses[clarificationId];
    if (!response?.trim()) {
      setMessage({ tone: 'danger', text: 'Enter a response before resolving a clarification.' });
      return;
    }
    try {
      await apiRequest(`/clarifications/${clarificationId}`, {
        method: 'PATCH',
        body: JSON.stringify({ response }),
      });
      setMessage({ tone: 'success', text: 'Clarification resolved.' });
      if (selectedId) loadClarifications(selectedId);
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Failed to resolve clarification.' });
    }
  };

  const handleStatusGate = async () => {
    if (!selectedId) return;
    try {
      const result = await apiRequest<{ passed: boolean; message: string }>(`/rfx-events/${selectedId}/status-gate-decision`, {
        method: 'POST',
        body: JSON.stringify({ decision: gateDecision, supplierId: gateSupplierId || undefined }),
      });
      setMessage({ tone: result.passed ? 'success' : 'danger', text: result.message });
    } catch (err: unknown) {
      setMessage({
        tone: 'danger',
        text: err instanceof Error ? err.message : 'Cannot proceed while unresolved material clarifications or deviations exist.',
      });
    }
  };

  return (
    <div>
      <PageHeader
        title="Evaluation Workspace"
        subtitle="Score technical/commercial/contract responses against the locked published RFx baseline, raise and resolve clarifications, and decide the status-gate."
      />

      {message && <StatusMessage tone={message.tone}>{message.text}</StatusMessage>}

      <Card title="Select RFx Event" className="mb-5">
        <TableWrap>
          <table className={tableClass}>
            <thead className={theadClass}>
              <tr className={trClass}>
                <th className={thClass}>RFx Event</th>
                <th className={thClass}>Tender Type</th>
                <th className={thClass}>Status</th>
              </tr>
            </thead>
            <tbody>
              {events.map((ev) => (
                <tr
                  key={ev.id}
                  className={`${trClass} cursor-pointer ${selectedId === ev.id ? 'bg-canvas-subtle' : ''}`}
                  onClick={() => setSelectedId(ev.id)}
                >
                  <td className={`${tdClass} font-mono text-2xs text-accent font-medium`}>{ev.id.slice(0, 8)}...</td>
                  <td className={tdClass}>{ev.tenderType}</td>
                  <td className={tdClass}>
                    <Badge tone="neutral">{ev.status}</Badge>
                  </td>
                </tr>
              ))}
              {events.length === 0 && (
                <tr className={trClass}>
                  <td className={`${tdClass} text-ink-muted`} colSpan={3}>
                    No RFx events found — create one in RFx Events first.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </TableWrap>
      </Card>

      {selectedId && (
        <div className="space-y-5">
          <Card title="Score Evaluation">
            <form onSubmit={handleScore} className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
              <div>
                <label htmlFor="supplierId" className={labelClass}>
                  Supplier ID *
                </label>
                <input id="supplierId" className={inputClass} value={supplierId} onChange={(e) => setSupplierId(e.target.value)} />
              </div>
              <div>
                <label htmlFor="evaluatorId" className={labelClass}>
                  Evaluator ID *
                </label>
                <input id="evaluatorId" className={inputClass} value={evaluatorId} onChange={(e) => setEvaluatorId(e.target.value)} />
              </div>
              <div>
                <label htmlFor="technicalScore" className={labelClass}>
                  Technical Score
                </label>
                <input
                  id="technicalScore"
                  type="number"
                  className={inputClass}
                  value={technicalScore}
                  onChange={(e) => setTechnicalScore(Number(e.target.value))}
                />
              </div>
              <div>
                <label htmlFor="commercialScore" className={labelClass}>
                  Commercial Score
                </label>
                <input
                  id="commercialScore"
                  type="number"
                  className={inputClass}
                  value={commercialScore}
                  onChange={(e) => setCommercialScore(Number(e.target.value))}
                />
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="comments" className={labelClass}>
                  Comments
                </label>
                <textarea id="comments" rows={2} className={inputClass} value={comments} onChange={(e) => setComments(e.target.value)} />
              </div>
              <div className="sm:col-span-2 flex items-center justify-between">
                <label className="text-sm text-ink inline-flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={lockOnSave} onChange={(e) => setLockOnSave(e.target.checked)} />
                  Lock evaluation (cannot be modified after)
                </label>
                <ButtonPrimary type="submit">Save Score</ButtonPrimary>
              </div>
            </form>

            <TableWrap>
              <table className={tableClass}>
                <thead className={theadClass}>
                  <tr className={trClass}>
                    <th className={thClass}>Supplier</th>
                    <th className={thClass}>Evaluator</th>
                    <th className={thClass}>Technical</th>
                    <th className={thClass}>Commercial</th>
                    <th className={thClass}>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {evaluations.map((ev) => (
                    <tr key={ev.id} className={trClass}>
                      <td className={`${tdClass} font-mono text-2xs`}>{ev.supplierId.slice(0, 8)}...</td>
                      <td className={tdClass}>{ev.evaluatorId}</td>
                      <td className={`${tdClass} tabular-nums`}>{ev.technicalScore}</td>
                      <td className={`${tdClass} tabular-nums`}>{ev.commercialScore}</td>
                      <td className={tdClass}>
                        <Badge tone={ev.status === 'locked' ? 'success' : 'neutral'}>{ev.status}</Badge>
                      </td>
                    </tr>
                  ))}
                  {evaluations.length === 0 && (
                    <tr className={trClass}>
                      <td className={`${tdClass} text-ink-muted`} colSpan={5}>
                        No evaluations scored yet.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </TableWrap>
          </Card>

          <Card title="Clarifications">
            <form onSubmit={handleRaiseClarification} className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
              <div>
                <label htmlFor="clarSupplierId" className={labelClass}>
                  Supplier ID *
                </label>
                <input id="clarSupplierId" className={inputClass} value={clarSupplierId} onChange={(e) => setClarSupplierId(e.target.value)} />
              </div>
              <div>
                <label htmlFor="clarCategory" className={labelClass}>
                  Category
                </label>
                <select id="clarCategory" className={inputClass} value={clarCategory} onChange={(e) => setClarCategory(e.target.value)}>
                  <option>Technical</option>
                  <option>Commercial</option>
                  <option>Contract</option>
                </select>
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="clarQuestion" className={labelClass}>
                  Question *
                </label>
                <textarea id="clarQuestion" rows={2} className={inputClass} value={clarQuestion} onChange={(e) => setClarQuestion(e.target.value)} />
              </div>
              <div className="sm:col-span-2 flex items-center justify-between">
                <label className="text-sm text-ink inline-flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={clarMaterial} onChange={(e) => setClarMaterial(e.target.checked)} />
                  Material deviation (blocks status-gate until resolved)
                </label>
                <ButtonPrimary type="submit">Raise Clarification</ButtonPrimary>
              </div>
            </form>

            <TableWrap>
              <table className={tableClass}>
                <thead className={theadClass}>
                  <tr className={trClass}>
                    <th className={thClass}>Supplier</th>
                    <th className={thClass}>Question</th>
                    <th className={thClass}>Status</th>
                    <th className={thClass}>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {clarifications.map((c) => (
                    <tr key={c.id} className={trClass}>
                      <td className={`${tdClass} font-mono text-2xs`}>{c.supplierId.slice(0, 8)}...</td>
                      <td className={tdClass}>
                        {c.question}
                        {c.isMaterialDeviation && (
                          <span className="ml-2">
                            <Badge tone="warning">material</Badge>
                          </span>
                        )}
                      </td>
                      <td className={tdClass}>
                        <Badge tone={c.status === 'resolved' ? 'success' : 'neutral'}>{c.status}</Badge>
                      </td>
                      <td className={tdClass}>
                        {c.status === 'pending' && (
                          <div className="flex gap-2">
                            <input
                              className={`${inputClass} w-40`}
                              placeholder="Response"
                              value={resolveResponses[c.id] ?? ''}
                              onChange={(e) => setResolveResponses((prev) => ({ ...prev, [c.id]: e.target.value }))}
                            />
                            <ButtonGhost onClick={() => handleResolve(c.id)}>Resolve</ButtonGhost>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                  {clarifications.length === 0 && (
                    <tr className={trClass}>
                      <td className={`${tdClass} text-ink-muted`} colSpan={4}>
                        No clarifications raised yet.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </TableWrap>
          </Card>

          <Card title="Status Gate Decision">
            <div className="flex flex-wrap items-end gap-3">
              <div className="w-64">
                <label htmlFor="gateSupplierId" className={labelClass}>
                  Supplier ID (optional)
                </label>
                <input id="gateSupplierId" className={inputClass} value={gateSupplierId} onChange={(e) => setGateSupplierId(e.target.value)} />
              </div>
              <div className="w-48">
                <label htmlFor="gateDecision" className={labelClass}>
                  Decision
                </label>
                <select
                  id="gateDecision"
                  className={inputClass}
                  value={gateDecision}
                  onChange={(e) => setGateDecision(e.target.value as 'proceed' | 'return' | 'reject')}
                >
                  <option value="proceed">Proceed</option>
                  <option value="return">Return</option>
                  <option value="reject">Reject</option>
                </select>
              </div>
              <ButtonPrimary onClick={handleStatusGate}>Submit Decision</ButtonPrimary>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
