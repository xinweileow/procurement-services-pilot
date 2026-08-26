import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';
import type { PagedResponse, RfxEventDetailResponse, RfxEventResponse } from '../../types/requests';
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

type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';

function statusTone(status: string): Tone {
  switch (status) {
    case 'published':
      return 'info';
    case 'opened':
      return 'success';
    case 'cancelled':
      return 'danger';
    case 'closed':
      return 'warning';
    default:
      return 'neutral';
  }
}

function formatDate(value: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleString();
}

export function RfxEvents() {
  const [events, setEvents] = useState<PagedResponse<RfxEventResponse> | null>(null);
  const [statusFilter, setStatusFilter] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<RfxEventDetailResponse | null>(null);
  const [message, setMessage] = useState<{ tone: Tone; text: string } | null>(null);

  const [showCreate, setShowCreate] = useState(false);
  const [sourcingStrategyId, setSourcingStrategyId] = useState('');
  const [technicalTemplateId, setTechnicalTemplateId] = useState('');
  const [commercialTemplateId, setCommercialTemplateId] = useState('');
  const [tenderType, setTenderType] = useState<'open' | 'invited'>('open');

  const [inviteInput, setInviteInput] = useState('');
  const [extendDate, setExtendDate] = useState('');
  const [extendReason, setExtendReason] = useState('');

  const loadEvents = () => {
    const params = new URLSearchParams();
    if (statusFilter) params.set('status', statusFilter);
    apiRequest<PagedResponse<RfxEventResponse>>(`/rfx-events?${params.toString()}`)
      .then(setEvents)
      .catch(() => {});
  };

  useEffect(() => {
    loadEvents();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter]);

  const loadDetail = (id: string) => {
    apiRequest<RfxEventDetailResponse>(`/rfx-events/${id}`)
      .then(setDetail)
      .catch(() => {});
  };

  useEffect(() => {
    if (selectedId) {
      loadDetail(selectedId);
    } else {
      setDetail(null);
    }
  }, [selectedId]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!sourcingStrategyId.trim()) {
      setMessage({ tone: 'danger', text: 'Sourcing Strategy ID is required to build an RFx event.' });
      return;
    }
    try {
      const created = await apiRequest<RfxEventResponse>('/rfx-events', {
        method: 'POST',
        body: JSON.stringify({
          sourcingStrategyId,
          technicalTemplateId: technicalTemplateId || undefined,
          commercialTemplateId: commercialTemplateId || undefined,
          tenderType,
        }),
      });
      setMessage({ tone: 'success', text: `RFx event created as draft (${created.id.slice(0, 8)}...).` });
      setShowCreate(false);
      setSourcingStrategyId('');
      setTechnicalTemplateId('');
      setCommercialTemplateId('');
      loadEvents();
      setSelectedId(created.id);
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Failed to create RFx event.' });
    }
  };

  const handlePublish = async () => {
    if (!detail) return;
    try {
      await apiRequest(`/rfx-events/${detail.id}/publish`, { method: 'POST' });
      setMessage({ tone: 'success', text: 'RFx event published.' });
      loadDetail(detail.id);
      loadEvents();
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Publish failed — check section completeness.' });
    }
  };

  const handleInvite = async () => {
    if (!detail) return;
    const supplierIds = inviteInput
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);
    if (supplierIds.length === 0) {
      setMessage({ tone: 'danger', text: 'Enter at least one supplier ID to invite.' });
      return;
    }
    try {
      await apiRequest(`/rfx-events/${detail.id}/invitations`, {
        method: 'POST',
        body: JSON.stringify({ supplierIds }),
      });
      setMessage({ tone: 'success', text: 'Supplier(s) invited.' });
      setInviteInput('');
      loadDetail(detail.id);
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Invitation failed.' });
    }
  };

  const handleExtend = async () => {
    if (!detail) return;
    if (!extendDate || !extendReason.trim()) {
      setMessage({ tone: 'danger', text: 'A new closing date and reason are both required to extend the deadline.' });
      return;
    }
    try {
      await apiRequest(`/rfx-events/${detail.id}/extend`, {
        method: 'POST',
        body: JSON.stringify({ newClosingDate: extendDate, reason: extendReason }),
      });
      setMessage({ tone: 'success', text: 'Submission deadline extended.' });
      setExtendDate('');
      setExtendReason('');
      loadDetail(detail.id);
      loadEvents();
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Extend deadline failed.' });
    }
  };

  const handleOpenProposals = async () => {
    if (!detail) return;
    try {
      await apiRequest(`/rfx-events/${detail.id}/open`, { method: 'POST' });
      setMessage({ tone: 'success', text: 'Sealed proposals opened.' });
      loadDetail(detail.id);
      loadEvents();
    } catch (err: unknown) {
      setMessage({ tone: 'danger', text: err instanceof Error ? err.message : 'Cannot open proposals before the closing deadline.' });
    }
  };

  return (
    <div>
      <PageHeader
        title="RFx Events"
        subtitle="Build, publish, and administer RFx events; open sealed submissions once the deadline has passed."
      />

      {message && <StatusMessage tone={message.tone}>{message.text}</StatusMessage>}

      <Card
        title="RFx Events"
        action={
          <ButtonPrimary onClick={() => setShowCreate((s) => !s)}>{showCreate ? 'Cancel' : 'New RFx Event'}</ButtonPrimary>
        }
        className="mb-5"
      >
        {showCreate && (
          <form onSubmit={handleCreate} className="mb-5 p-4 bg-canvas-subtle rounded-md border border-line">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
              <div>
                <label htmlFor="sourcingStrategyId" className={labelClass}>
                  Sourcing Strategy ID *
                </label>
                <input
                  id="sourcingStrategyId"
                  className={inputClass}
                  value={sourcingStrategyId}
                  onChange={(e) => setSourcingStrategyId(e.target.value)}
                  placeholder="Approved sourcing strategy UUID"
                />
              </div>
              <div>
                <label htmlFor="tenderType" className={labelClass}>
                  Tender Type
                </label>
                <select id="tenderType" className={inputClass} value={tenderType} onChange={(e) => setTenderType(e.target.value as 'open' | 'invited')}>
                  <option value="open">Open</option>
                  <option value="invited">Invited</option>
                </select>
              </div>
              <div>
                <label htmlFor="technicalTemplateId" className={labelClass}>
                  Technical Template ID
                </label>
                <input
                  id="technicalTemplateId"
                  className={inputClass}
                  value={technicalTemplateId}
                  onChange={(e) => setTechnicalTemplateId(e.target.value)}
                  placeholder="Required before publish"
                />
              </div>
              <div>
                <label htmlFor="commercialTemplateId" className={labelClass}>
                  Commercial Template ID
                </label>
                <input
                  id="commercialTemplateId"
                  className={inputClass}
                  value={commercialTemplateId}
                  onChange={(e) => setCommercialTemplateId(e.target.value)}
                  placeholder="Required before publish"
                />
              </div>
            </div>
            <ButtonPrimary type="submit">Create Draft</ButtonPrimary>
          </form>
        )}

        <div className="flex flex-wrap items-end gap-3 mb-4">
          <div className="w-48">
            <label htmlFor="rfxStatusFilter" className={labelClass}>
              Status
            </label>
            <select id="rfxStatusFilter" className={inputClass} value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
              <option value="">All</option>
              <option value="draft">Draft</option>
              <option value="published">Published</option>
              <option value="opened">Opened</option>
              <option value="closed">Closed</option>
              <option value="cancelled">Cancelled</option>
            </select>
          </div>
        </div>

        {events && (
          <TableWrap>
            <table className={tableClass}>
              <thead className={theadClass}>
                <tr className={trClass}>
                  <th className={thClass}>RFx Event</th>
                  <th className={thClass}>Tender Type</th>
                  <th className={thClass}>Status</th>
                  <th className={thClass}>Closing</th>
                </tr>
              </thead>
              <tbody>
                {events.items.map((ev) => (
                  <tr
                    key={ev.id}
                    className={`${trClass} cursor-pointer ${selectedId === ev.id ? 'bg-canvas-subtle' : ''}`}
                    onClick={() => setSelectedId(ev.id)}
                  >
                    <td className={`${tdClass} font-mono text-2xs text-accent font-medium`}>{ev.id.slice(0, 8)}...</td>
                    <td className={tdClass}>{ev.tenderType}</td>
                    <td className={tdClass}>
                      <Badge tone={statusTone(ev.status)}>{ev.status}</Badge>
                    </td>
                    <td className={tdClass}>{formatDate(ev.closingDateUtc)}</td>
                  </tr>
                ))}
                {events.items.length === 0 && (
                  <tr className={trClass}>
                    <td className={`${tdClass} text-ink-muted`} colSpan={4}>
                      No RFx events yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </TableWrap>
        )}
      </Card>

      {detail && (
        <Card
          title={`RFx Event — ${detail.id.slice(0, 8)}...`}
          action={<Badge tone={statusTone(detail.status)}>{detail.status}</Badge>}
        >
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-5">
            <div className="bg-canvas-subtle rounded-md px-3 py-2.5">
              <div className="text-2xs text-ink-muted">Tender Type</div>
              <div className="font-bold text-sm mt-0.5 text-ink">{detail.tenderType}</div>
            </div>
            <div className="bg-canvas-subtle rounded-md px-3 py-2.5">
              <div className="text-2xs text-ink-muted">Opening</div>
              <div className="font-bold text-sm mt-0.5 text-ink">{formatDate(detail.openingDateUtc)}</div>
            </div>
            <div className="bg-canvas-subtle rounded-md px-3 py-2.5">
              <div className="text-2xs text-ink-muted">Closing</div>
              <div className="font-bold text-sm mt-0.5 text-ink">{formatDate(detail.closingDateUtc)}</div>
            </div>
            <div className="bg-canvas-subtle rounded-md px-3 py-2.5">
              <div className="text-2xs text-ink-muted">Invited Suppliers</div>
              <div className="font-bold text-sm mt-0.5 text-ink">{detail.invitedSupplierIds.length}</div>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 mb-5">
            <div className="space-y-3">
              <ButtonPrimary onClick={handlePublish} disabled={detail.status !== 'draft'} className="w-full py-2">
                Publish
              </ButtonPrimary>

              <div className="pt-2 border-t border-line">
                <label htmlFor="inviteInput" className={labelClass}>
                  Invite Suppliers (comma-separated supplier IDs)
                </label>
                <div className="flex gap-2">
                  <input id="inviteInput" className={inputClass} value={inviteInput} onChange={(e) => setInviteInput(e.target.value)} />
                  <ButtonGhost onClick={handleInvite}>Invite</ButtonGhost>
                </div>
              </div>

              <div className="pt-2 border-t border-line">
                <label htmlFor="extendDate" className={labelClass}>
                  Extend Deadline
                </label>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 mb-2">
                  <input id="extendDate" type="datetime-local" className={inputClass} value={extendDate} onChange={(e) => setExtendDate(e.target.value)} />
                  <input
                    id="extendReason"
                    className={inputClass}
                    value={extendReason}
                    onChange={(e) => setExtendReason(e.target.value)}
                    placeholder="Reason (required)"
                  />
                </div>
                <ButtonGhost onClick={handleExtend}>Extend Deadline</ButtonGhost>
              </div>
            </div>

            <div>
              <div className="flex items-center justify-between mb-2">
                <h3 className="text-[0.9375rem] font-semibold text-ink">Submissions</h3>
                <ButtonPrimary onClick={handleOpenProposals} disabled={detail.status === 'opened' || detail.status === 'draft'}>
                  Open Proposals
                </ButtonPrimary>
              </div>
              <TableWrap>
                <table className={tableClass}>
                  <thead className={theadClass}>
                    <tr className={trClass}>
                      <th className={thClass}>Supplier</th>
                      <th className={thClass}>Bid Status</th>
                      <th className={thClass}>Submitted</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.submissions.map((s) => (
                      <tr key={s.id} className={trClass}>
                        <td className={`${tdClass} font-mono text-2xs`}>{s.supplierId.slice(0, 8)}...</td>
                        <td className={tdClass}>
                          <Badge tone={s.bidStatus === 'opened' ? 'success' : s.bidStatus === 'disqualified' ? 'danger' : 'neutral'}>
                            {s.bidStatus}
                          </Badge>
                        </td>
                        <td className={tdClass}>{formatDate(s.submittedAt)}</td>
                      </tr>
                    ))}
                    {detail.submissions.length === 0 && (
                      <tr className={trClass}>
                        <td className={`${tdClass} text-ink-muted`} colSpan={3}>
                          No submissions yet.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </TableWrap>
            </div>
          </div>
        </Card>
      )}
    </div>
  );
}
