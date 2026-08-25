import { useState } from 'react';
import { CatalogueSearch } from './CatalogueSearch';
import { RequestLanding } from './RequestLanding';
import type { RequestResponse } from '../../types/requests';

export function RequestorIntakeWizard() {
  const [step, setStep] = useState<'catalogue' | 'details' | 'success'>('catalogue');
  const [submittedRequest, setSubmittedRequest] = useState<RequestResponse | null>(null);

  return (
    <div>
      <nav aria-label="Intake wizard steps">
        <span style={{ fontWeight: step === 'catalogue' ? 'bold' : 'normal' }}>1. Catalogue Search</span> &gt;{' '}
        <span style={{ fontWeight: step === 'details' ? 'bold' : 'normal' }}>2. Request Details</span> &gt;{' '}
        <span style={{ fontWeight: step === 'success' ? 'bold' : 'normal' }}>3. Submission Outcome</span>
      </nav>

      {step === 'catalogue' && (
        <CatalogueSearch onSelectNonCatalogue={() => setStep('details')} />
      )}

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
          <h2>Request Submitted Successfully</h2>
          <dl>
            <dt>Request ID</dt>
            <dd>{submittedRequest.requestId}</dd>
            <dt>Status</dt>
            <dd>{submittedRequest.status}</dd>
            <dt>Routing Destination</dt>
            <dd>{submittedRequest.routingDestination}</dd>
          </dl>
          {submittedRequest.routingDestination === 'Gsp' && (
            <p>Since the estimated value is RM100k and above in Malaysia, the request has been routed to GSP for strategic assessment.</p>
          )}
          {submittedRequest.routingDestination === 'EtiqaInternalProcurement' && (
            <p>The request has been routed to Etiqa Internal Procurement Team.</p>
          )}
        </section>
      )}
    </div>
  );
}
