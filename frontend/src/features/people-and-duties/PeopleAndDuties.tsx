import { useState } from 'react';
import { apiRequest } from '../../api/client';

interface Props {
  requestId: string;
  onContinue: () => void;
}

interface RoleAssignmentsResponse {
  id: string;
  requestId: string;
  technicalContact?: string;
  procurementLead: string;
  technicalEvaluator: string;
  commercialEvaluator: string;
  approver: string;
  sodConflict: boolean;
}

export function PeopleAndDuties({ requestId, onContinue }: Props) {
  const [technicalContact, setTechnicalContact] = useState('');
  const [procurementLead, setProcurementLead] = useState('');
  const [technicalEvaluator, setTechnicalEvaluator] = useState('');
  const [commercialEvaluator, setCommercialEvaluator] = useState('');
  const [approver, setApprover] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);

  const getSodConflict = () => {
    const list = [procurementLead, technicalEvaluator, commercialEvaluator, approver]
      .map((n) => n.trim().toLowerCase())
      .filter(Boolean);
    return new Set(list).size !== list.length;
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSaveMessage(null);

    if (!procurementLead.trim() || !technicalEvaluator.trim() || !commercialEvaluator.trim() || !approver.trim()) {
      setError('Procurement lead, technical evaluator, commercial evaluator and approver are required.');
      return;
    }

    if (getSodConflict()) {
      setError('Resolve segregation of duties conflict: all assigned evaluators and approvers must be distinct individuals.');
      return;
    }

    try {
      await apiRequest<RoleAssignmentsResponse>(`/requests/${requestId}/role-assignments`, {
        method: 'PATCH',
        body: JSON.stringify({
          technicalContact,
          procurementLead,
          technicalEvaluator,
          commercialEvaluator,
          approver,
        }),
      });
      setSaveMessage('Role assignments saved successfully.');
      onContinue();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Save failed');
    }
  };

  const conflict = getSodConflict();

  return (
    <form onSubmit={handleSave}>
      <h3>People & Duties Role Assignments</h3>

      {error && <div role="alert" style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}
      {saveMessage && <div role="status" style={{ color: 'green', marginBottom: '1rem' }}>{saveMessage}</div>}

      {conflict && (
        <div role="alert" style={{ background: '#fff0ee', color: '#b42318', padding: '0.75rem', borderRadius: '8px', marginBottom: '1rem' }}>
          <strong>Segregation of duties conflict detected</strong>
          <div style={{ fontSize: '0.85rem', marginTop: '0.25rem' }}>
            Procurement lead, technical evaluator, commercial evaluator and approver must be assigned to different distinct users.
          </div>
        </div>
      )}

      {!conflict && procurementLead && technicalEvaluator && commercialEvaluator && approver && (
        <div style={{ background: '#eaf8f1', color: '#137a4b', padding: '0.75rem', borderRadius: '8px', marginBottom: '1rem' }}>
          <strong>Segregation of duties satisfied</strong>
        </div>
      )}

      <div>
        <label htmlFor="technicalContactInput">Technical contact</label>
        <input
          id="technicalContactInput"
          value={technicalContact}
          onChange={(e) => setTechnicalContact(e.target.value)}
        />
      </div>

      <div style={{ marginTop: '0.5rem' }}>
        <label htmlFor="procurementLeadInput">Procurement lead *</label>
        <input
          id="procurementLeadInput"
          value={procurementLead}
          onChange={(e) => setProcurementLead(e.target.value)}
        />
      </div>

      <div style={{ marginTop: '0.5rem' }}>
        <label htmlFor="technicalEvaluatorInput">Technical evaluator *</label>
        <input
          id="technicalEvaluatorInput"
          value={technicalEvaluator}
          onChange={(e) => setTechnicalEvaluator(e.target.value)}
        />
      </div>

      <div style={{ marginTop: '0.5rem' }}>
        <label htmlFor="commercialEvaluatorInput">Commercial evaluator *</label>
        <input
          id="commercialEvaluatorInput"
          value={commercialEvaluator}
          onChange={(e) => setCommercialEvaluator(e.target.value)}
        />
      </div>

      <div style={{ marginTop: '0.5rem' }}>
        <label htmlFor="approverInput">Approver *</label>
        <input
          id="approverInput"
          value={approver}
          onChange={(e) => setApprover(e.target.value)}
        />
      </div>

      <div style={{ marginTop: '1rem' }}>
        <button type="submit">Save assignments</button>
      </div>
    </form>
  );
}
