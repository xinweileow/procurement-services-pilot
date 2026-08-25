import { useState } from 'react';
import { CatalogueSearch } from './CatalogueSearch';
import { RequestLanding } from './RequestLanding';
import type { RequestResponse } from '../../types/requests';
import { Card, PageHeader, StatusMessage } from '../../components/ui';

const STEPS: Array<{ id: 'catalogue' | 'details' | 'success'; label: string }> = [
  { id: 'catalogue', label: 'Catalogue Search' },
  { id: 'details', label: 'Request Details' },
  { id: 'success', label: 'Submission Outcome' },
];

export function RequestorIntakeWizard() {
  const [step, setStep] = useState<'catalogue' | 'details' | 'success'>('catalogue');
  const [submittedRequest, setSubmittedRequest] = useState<RequestResponse | null>(null);
  const activeIndex = STEPS.findIndex((s) => s.id === step);

  return (
    <div>
      <PageHeader title="New Request (Intake)" />

      <nav aria-label="Intake wizard steps" className="flex items-center gap-1 border-b border-line mb-5">
        {STEPS.map((s, i) => (
          <span
            key={s.id}
            className={
              'inline-flex items-center gap-1.5 px-3.5 py-2 text-[0.8125rem] font-medium border-b-2 -mb-px ' +
              (i === activeIndex ? 'border-accent text-ink font-semibold' : 'border-transparent text-ink-muted')
            }
          >
            {i + 1}. {s.label}
          </span>
        ))}
      </nav>

      {step === 'catalogue' && <CatalogueSearch onSelectNonCatalogue={() => setStep('details')} />}

      {step === 'details' && (
        <RequestLanding
          onSubmitted={(req) => {
            setSubmittedRequest(req);
            setStep('success');
          }}
        />
      )}

      {step === 'success' && submittedRequest && (
        <section role="status">
          <Card title="Request Submitted Successfully">
            <StatusMessage tone="success">
              <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
                <dt className="text-ink-muted">Request ID</dt>
                <dd className="m-0 font-medium">{submittedRequest.requestId}</dd>
                <dt className="text-ink-muted">Status</dt>
                <dd className="m-0 font-medium">{submittedRequest.status}</dd>
                <dt className="text-ink-muted">Routing Destination</dt>
                <dd className="m-0 font-medium">{submittedRequest.routingDestination}</dd>
              </dl>
            </StatusMessage>
            {submittedRequest.routingDestination === 'Gsp' && (
              <p className="text-sm text-ink-muted mt-3">
                Since the estimated value is RM100k and above in Malaysia, the request has been routed to GSP for strategic assessment.
              </p>
            )}
            {submittedRequest.routingDestination === 'EtiqaInternalProcurement' && (
              <p className="text-sm text-ink-muted mt-3">The request has been routed to Etiqa Internal Procurement Team.</p>
            )}
          </Card>
        </section>
      )}
    </div>
  );
}
