import { useState } from 'react';
import { apiRequest } from '../../api/client';

interface Props {
  requestId: string;
  onContinue: () => void;
}

export function GovernanceDeclarations({ requestId, onContinue }: Props) {
  const [outsourcing, setOutsourcing] = useState(false);
  const [singleSource, setSingleSource] = useState(false);
  const [emergency, setEmergency] = useState(false);
  const [singleSourceCategory, setSingleSourceCategory] = useState('Proprietary Solution');
  const [singleSourceReason, setSingleSourceReason] = useState('');
  const [emergencyReason, setEmergencyReason] = useState('');
  const [disruptionImpact, setDisruptionImpact] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSaveMessage(null);

    if (singleSource && !singleSourceReason.trim()) {
      setError('Single-source justification reason is mandatory.');
      return;
    }
    if (emergency && (!emergencyReason.trim() || !disruptionImpact.trim())) {
      setError('Emergency reason and disruption impact are mandatory.');
      return;
    }

    try {
      await apiRequest(`/requests/${requestId}/governance-declarations`, {
        method: 'PATCH',
        body: JSON.stringify({
          outsourcing,
          singleSource,
          emergency,
          singleSourceCategory: singleSource ? singleSourceCategory : null,
          singleSourceReason: singleSource ? singleSourceReason : null,
          emergencyReason: emergency ? emergencyReason : null,
          disruptionImpact: emergency ? disruptionImpact : null,
        }),
      });
      setSaveMessage('Declarations saved successfully.');
      onContinue();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Save failed');
    }
  };

  return (
    <form onSubmit={handleSave}>
      <h3>Governance Declarations</h3>

      {error && <div role="alert" style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}
      {saveMessage && <div role="status" style={{ color: 'green', marginBottom: '1rem' }}>{saveMessage}</div>}

      <div>
        <label>
          <input
            id="outsourcingInput"
            type="checkbox"
            checked={outsourcing}
            onChange={(e) => setOutsourcing(e.target.checked)}
          />
          Is this an outsourcing engagement?
        </label>
      </div>

      <div style={{ marginTop: '0.5rem' }}>
        <label>
          <input
            id="singleSourceInput"
            type="checkbox"
            checked={singleSource}
            onChange={(e) => setSingleSource(e.target.checked)}
          />
          Is this intended for a specific supplier / single-source scenario?
        </label>
      </div>

      {singleSource && (
        <div style={{ marginLeft: '1.5rem', marginTop: '0.5rem' }}>
          <div>
            <label htmlFor="singleSourceCategoryInput">Single-source category *</label>
            <select
              id="singleSourceCategoryInput"
              value={singleSourceCategory}
              onChange={(e) => setSingleSourceCategory(e.target.value)}
            >
              <option>Proprietary Solution</option>
              <option>Regulatory Requirement</option>
              <option>Sole Supplier</option>
              <option>Maintenance Continuity</option>
            </select>
          </div>
          <div style={{ marginTop: '0.5rem' }}>
            <label htmlFor="singleSourceReasonInput">Single-source justification *</label>
            <textarea
              id="singleSourceReasonInput"
              value={singleSourceReason}
              onChange={(e) => setSingleSourceReason(e.target.value)}
            />
          </div>
        </div>
      )}

      <div style={{ marginTop: '0.5rem' }}>
        <label>
          <input
            id="emergencyInput"
            type="checkbox"
            checked={emergency}
            onChange={(e) => setEmergency(e.target.checked)}
          />
          Is this an emergency procurement?
        </label>
      </div>

      {emergency && (
        <div style={{ marginLeft: '1.5rem', marginTop: '0.5rem' }}>
          <div>
            <label htmlFor="emergencyReasonInput">Emergency reason *</label>
            <textarea
              id="emergencyReasonInput"
              value={emergencyReason}
              onChange={(e) => setEmergencyReason(e.target.value)}
            />
          </div>
          <div style={{ marginTop: '0.5rem' }}>
            <label htmlFor="disruptionImpactInput">Disruption impact *</label>
            <textarea
              id="disruptionImpactInput"
              value={disruptionImpact}
              onChange={(e) => setDisruptionImpact(e.target.value)}
            />
          </div>
        </div>
      )}

      <div style={{ marginTop: '1rem' }}>
        <button type="submit">Save declarations</button>
      </div>
    </form>
  );
}
