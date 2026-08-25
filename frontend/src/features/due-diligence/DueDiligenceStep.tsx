import { useState } from 'react';

export interface DueDiligenceStepProps {
  initialSourcingType?: 'Sourceable' | 'Non-Sourceable';
  initialSpendType?: 'Addressable Spend' | 'Non-Addressable Spend';
  onApplicabilityChange?: (sourcingType: string, spendType: string) => void;
}

export function DueDiligenceStep({
  initialSourcingType = 'Sourceable',
  initialSpendType = 'Addressable Spend',
  onApplicabilityChange,
}: DueDiligenceStepProps) {
  const [sourcingType, setSourcingType] = useState<'Sourceable' | 'Non-Sourceable'>(initialSourcingType);
  const [spendType, setSpendType] = useState<'Addressable Spend' | 'Non-Addressable Spend'>(initialSpendType);
  const [hasSystemAccess, setHasSystemAccess] = useState(false);
  const [hasDataAccess, setHasDataAccess] = useState(false);

  const isAddressable = spendType === 'Addressable Spend';
  const isSourceable = sourcingType === 'Sourceable';

  // Applicability computation
  const threePCRequired = isAddressable;
  const esgRequired = isSourceable && isAddressable;
  const associatedPersonRequired = isAddressable;
  const isMaterial = hasSystemAccess || hasDataAccess;

  // Assessment tile states
  const threePCStatus = threePCRequired ? 'Ready' : 'Not applicable';
  const esgStatus = esgRequired ? 'Ready' : 'Not applicable';
  const associatedPersonStatus = associatedPersonRequired ? 'Ready' : 'Not applicable';
  const tprmStatus = isMaterial ? 'Review required' : 'Ready';

  const isReady =
    (!threePCRequired || threePCStatus === 'Ready') &&
    (!esgRequired || esgStatus === 'Ready') &&
    (!associatedPersonRequired || associatedPersonStatus === 'Ready') &&
    (!isMaterial || tprmStatus === 'Ready');

  const getRuleText = () => {
    if (isSourceable && isAddressable) {
      return 'Rule A1: Full Sourcing Due Diligence Required (3PC, ESG, Associated Person, and TPRM if material).';
    }
    if (!isSourceable && isAddressable) {
      return 'Rule B2: Non-Sourceable Addressable Spend requires 3PC & Associated Person verification. ESG assessment is not applicable.';
    }
    return 'Rule C3: Non-Addressable Spend is exempt from standard supplier sourcing due diligence.';
  };

  const handleSourcingTypeChange = (val: 'Sourceable' | 'Non-Sourceable') => {
    setSourcingType(val);
    if (onApplicabilityChange) onApplicabilityChange(val, spendType);
  };

  const handleSpendTypeChange = (val: 'Addressable Spend' | 'Non-Addressable Spend') => {
    setSpendType(val);
    if (onApplicabilityChange) onApplicabilityChange(sourcingType, val);
  };

  return (
    <div style={{ maxWidth: '950px', margin: '0 auto' }}>
      <h2>Supplier Due Diligence & Applicability Assessment</h2>

      {/* Policy banner */}
      <div style={{ padding: '1rem', background: '#e7f1ff', border: '1px solid #b8daff', borderRadius: '4px', marginBottom: '1.5rem' }}>
        <h4 style={{ margin: '0 0 0.5rem 0', color: '#004085' }}>Procurement Policy Guidance</h4>
        <p style={{ margin: 0, fontSize: '0.9rem', color: '#004085' }}>
          Due diligence requirements are determined dynamically by Sourcing Type (Sourceable vs. Non-Sourceable) and
          Spend Type (Addressable vs. Non-Addressable spend).
        </p>
      </div>

      {/* Applicability form */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem', marginBottom: '1.5rem' }}>
        <div>
          <label htmlFor="sourcingTypeSelect" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
            Sourcing Type
          </label>
          <select
            id="sourcingTypeSelect"
            value={sourcingType}
            onChange={(e) => handleSourcingTypeChange(e.target.value as 'Sourceable' | 'Non-Sourceable')}
            style={{ width: '100%', padding: '0.5rem' }}
          >
            <option value="Sourceable">Sourceable</option>
            <option value="Non-Sourceable">Non-Sourceable</option>
          </select>
        </div>

        <div>
          <label htmlFor="spendTypeSelect" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
            Spend Type
          </label>
          <select
            id="spendTypeSelect"
            value={spendType}
            onChange={(e) => handleSpendTypeChange(e.target.value as 'Addressable Spend' | 'Non-Addressable Spend')}
            style={{ width: '100%', padding: '0.5rem' }}
          >
            <option value="Addressable Spend">Addressable Spend</option>
            <option value="Non-Addressable Spend">Non-Addressable Spend</option>
          </select>
        </div>
      </div>

      {/* Applicable rule callout */}
      <div
        data-testid="applicable-rule-callout"
        style={{ padding: '0.75rem 1rem', background: '#f8f9fa', borderLeft: '4px solid #17a2b8', marginBottom: '1.5rem' }}
      >
        <strong>Applicable Rule:</strong> <span style={{ color: '#333' }}>{getRuleText()}</span>
      </div>

      {/* Materiality flags */}
      <div style={{ marginBottom: '1.5rem', padding: '1rem', border: '1px solid #eee', borderRadius: '4px' }}>
        <h4 style={{ margin: '0 0 0.5rem 0' }}>Materiality & System/Data Access Flags</h4>
        <label style={{ display: 'inline-block', marginRight: '1.5rem', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={hasSystemAccess}
            onChange={(e) => setHasSystemAccess(e.target.checked)}
          />{' '}
          Supplier requires access to Etiqa Internal IT Systems
        </label>
        <label style={{ cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={hasDataAccess}
            onChange={(e) => setHasDataAccess(e.target.checked)}
          />{' '}
          Supplier processes Customer / Sensitive Personal Data
        </label>
        <div style={{ marginTop: '0.5rem', fontSize: '0.85rem', color: '#666' }}>
          Materiality Status: <strong style={{ color: isMaterial ? '#dc3545' : '#28a745' }}>{isMaterial ? 'Material Supplier (Enhanced TPRM required)' : 'Non-Material'}</strong>
        </div>
      </div>

      {/* 4 Assessment Tiles */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem', marginBottom: '1.5rem' }}>
        {/* Tile 1: 3PC */}
        <div data-testid="tile-3pc" style={{ padding: '1rem', border: '1px solid #ddd', borderRadius: '4px', background: '#fff' }}>
          <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>3-Point Check</div>
          <div style={{ fontSize: '0.8rem', color: '#666' }}>Identity & Registration</div>
          <div style={{ marginTop: '0.75rem' }}>
            <span
              data-testid="badge-3pc-req"
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: threePCRequired ? '#cce5ff' : '#e2e3e5',
                color: threePCRequired ? '#004085' : '#383d41',
                marginRight: '0.25rem',
              }}
            >
              {threePCRequired ? 'Required' : 'Not applicable'}
            </span>
            <span
              data-testid="badge-3pc-ready"
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: threePCStatus === 'Ready' ? '#d4edda' : '#fff3cd',
                color: threePCStatus === 'Ready' ? '#155724' : '#856404',
              }}
            >
              {threePCStatus}
            </span>
          </div>
        </div>

        {/* Tile 2: ESG */}
        <div data-testid="tile-esg" style={{ padding: '1rem', border: '1px solid #ddd', borderRadius: '4px', background: '#fff' }}>
          <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>ESG Assessment</div>
          <div style={{ fontSize: '0.8rem', color: '#666' }}>Sustainability & Governance</div>
          <div style={{ marginTop: '0.75rem' }}>
            <span
              data-testid="badge-esg-req"
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: esgRequired ? '#cce5ff' : '#e2e3e5',
                color: esgRequired ? '#004085' : '#383d41',
                marginRight: '0.25rem',
              }}
            >
              {esgRequired ? 'Required' : 'Not applicable'}
            </span>
            <span
              data-testid="badge-esg-ready"
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: esgStatus === 'Ready' ? '#d4edda' : '#fff3cd',
                color: esgStatus === 'Ready' ? '#155724' : '#856404',
              }}
            >
              {esgStatus}
            </span>
          </div>
        </div>

        {/* Tile 3: Associated Person */}
        <div data-testid="tile-associated-person" style={{ padding: '1rem', border: '1px solid #ddd', borderRadius: '4px', background: '#fff' }}>
          <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>Associated Person</div>
          <div style={{ fontSize: '0.8rem', color: '#666' }}>Anti-Bribery S.17A Check</div>
          <div style={{ marginTop: '0.75rem' }}>
            <span
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: associatedPersonRequired ? '#cce5ff' : '#e2e3e5',
                color: associatedPersonRequired ? '#004085' : '#383d41',
                marginRight: '0.25rem',
              }}
            >
              {associatedPersonRequired ? 'Required' : 'Not applicable'}
            </span>
            <span
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: associatedPersonStatus === 'Ready' ? '#d4edda' : '#fff3cd',
                color: associatedPersonStatus === 'Ready' ? '#155724' : '#856404',
              }}
            >
              {associatedPersonStatus}
            </span>
          </div>
        </div>

        {/* Tile 4: TPRM */}
        <div data-testid="tile-tprm" style={{ padding: '1rem', border: '1px solid #ddd', borderRadius: '4px', background: '#fff' }}>
          <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>TPRM Review</div>
          <div style={{ fontSize: '0.8rem', color: '#666' }}>Third-Party Risk Review</div>
          <div style={{ marginTop: '0.75rem' }}>
            <span
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: isMaterial ? '#cce5ff' : '#e2e3e5',
                color: isMaterial ? '#004085' : '#383d41',
                marginRight: '0.25rem',
              }}
            >
              {isMaterial ? 'Required' : 'Not applicable'}
            </span>
            <span
              style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 'bold',
                background: tprmStatus === 'Ready' ? '#d4edda' : '#f8d7da',
                color: tprmStatus === 'Ready' ? '#155724' : '#721c24',
              }}
            >
              {tprmStatus}
            </span>
          </div>
        </div>
      </div>

      {/* Overall readiness */}
      <div
        style={{
          padding: '1rem',
          borderRadius: '4px',
          background: isReady ? '#d4edda' : '#fff3cd',
          color: isReady ? '#155724' : '#856404',
          border: `1px solid ${isReady ? '#c3e6cb' : '#ffeeba'}`,
        }}
      >
        <strong>Overall Due Diligence Status:</strong>{' '}
        {isReady
          ? 'Ready — All applicable due diligence checks and evidence verified.'
          : 'Review Required — Mandatory checks or material supplier documentation pending.'}
      </div>
    </div>
  );
}
