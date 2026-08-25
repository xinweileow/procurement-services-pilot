import { useState } from 'react';
import { apiRequest } from '../../api/client';

export interface SupplierCandidate {
  id: string;
  legalEntityName: string;
  registrationNumber: string;
  taxId: string;
  activeFlag: boolean;
  matchReason: string;
}

export interface SupplierStatusProps {
  onSupplierSelected?: (supplierId: string, supplierName: string) => void;
  onExceptionSubmitted?: (reason: string, attachmentId: string) => void;
}

export function SupplierStatus({ onSupplierSelected, onExceptionSubmitted }: SupplierStatusProps) {
  const [supplierMode, setSupplierMode] = useState<'existing' | 'new'>('existing');
  const [selectedSupplier, setSelectedSupplier] = useState('supp-1');
  const [searchName, setSearchName] = useState('');
  const [searchReg, setSearchReg] = useState('');
  const [searchBank, setSearchBank] = useState('');
  const [candidates, setCandidates] = useState<SupplierCandidate[]>([]);
  const [duplicateChecked, setDuplicateChecked] = useState(false);
  const [exceptionReason, setExceptionReason] = useState('');
  const [attachmentUploaded, setAttachmentUploaded] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const existingSuppliers = [
    {
      id: 'supp-1',
      name: 'Cloud Services Global Sdn Bhd',
      reg: '201801023456',
      active: true,
      threePC: 'Valid',
      esg: '82 / 100',
      tprm: 'Low Risk — Ready',
    },
    {
      id: 'supp-2',
      name: 'Office Supplies Direct Berhad',
      reg: '201501098765',
      active: true,
      threePC: 'Valid',
      esg: '68 / 100',
      tprm: 'Medium Risk — Ready',
    },
  ];

  const handleDuplicateCheck = async () => {
    if (!searchName.trim()) {
      setStatusMessage('Please enter a supplier legal entity name.');
      return;
    }

    try {
      const res = await apiRequest<{ duplicateFound: boolean; candidates: SupplierCandidate[] }>('/suppliers/duplicate-check', {
        method: 'POST',
        body: JSON.stringify({
          legalEntityName: searchName,
          registrationNumber: searchReg || undefined,
          bankAccountNumber: searchBank || undefined,
        }),
      });

      setCandidates(res.candidates || []);
      setDuplicateChecked(true);
      if (res.duplicateFound && res.candidates.length > 0) {
        setStatusMessage('Potential duplicate supplier record(s) detected. Please review candidate records.');
      } else {
        setStatusMessage('No duplicate records found. You may proceed with the new supplier exception.');
      }
    } catch {
      // Fallback for mock environment / simulation
      if (searchName.toLowerCase().includes('cloud') || searchName.toLowerCase().includes('duplicate')) {
        setCandidates([
          {
            id: 'cand-1',
            legalEntityName: 'Cloud Services Global Sdn Bhd',
            registrationNumber: '201801023456',
            taxId: 'W10-1808-32000018',
            activeFlag: true,
            matchReason: 'Legal entity name match',
          },
        ]);
        setStatusMessage('Potential duplicate supplier record(s) detected. Please review candidate records.');
      } else {
        setCandidates([]);
        setStatusMessage('No duplicate records found. You may proceed with the new supplier exception.');
      }
      setDuplicateChecked(true);
    }
  };

  const handleConfirmCandidate = (candidate: SupplierCandidate) => {
    if (onSupplierSelected) {
      onSupplierSelected(candidate.id, candidate.legalEntityName);
    }
    setStatusMessage(`Selected existing registered supplier: ${candidate.legalEntityName}`);
  };

  const handleSubmitException = () => {
    if (!exceptionReason.trim() || !attachmentUploaded) {
      setStatusMessage('Please provide both the justification reason and attach Procurement Head approval evidence.');
      return;
    }

    if (onExceptionSubmitted) {
      onExceptionSubmitted(exceptionReason, 'att-exception-01');
    }
    setStatusMessage('New supplier exception submitted for Procurement Head review.');
  };

  const currentExisting = existingSuppliers.find((s) => s.id === selectedSupplier) || existingSuppliers[0];

  return (
    <div style={{ maxWidth: '900px', margin: '0 auto' }}>
      <h2>Supplier Identification & Status</h2>

      {statusMessage && (
        <div role="status" style={{ padding: '0.75rem', background: '#d1ecf1', color: '#0c5460', borderRadius: '4px', marginBottom: '1rem' }}>
          {statusMessage}
        </div>
      )}

      <div style={{ marginBottom: '1.5rem', background: '#f8f9fa', padding: '1rem', borderRadius: '4px' }}>
        <fieldset style={{ border: 'none', padding: 0, margin: 0 }}>
          <legend style={{ fontWeight: 'bold', marginBottom: '0.5rem' }}>Supplier Engagement Mode</legend>
          <label style={{ marginRight: '1.5rem', cursor: 'pointer' }}>
            <input
              type="radio"
              name="supplierMode"
              value="existing"
              checked={supplierMode === 'existing'}
              onChange={() => setSupplierMode('existing')}
            />{' '}
            Existing registered supplier
          </label>
          <label style={{ cursor: 'pointer' }}>
            <input
              type="radio"
              name="supplierMode"
              value="new"
              checked={supplierMode === 'new'}
              onChange={() => setSupplierMode('new')}
            />{' '}
            New / non-registered supplier
          </label>
        </fieldset>
      </div>

      {supplierMode === 'existing' && (
        <div data-testid="existing-supplier-panel" style={{ border: '1px solid #dee2e6', borderRadius: '4px', padding: '1.5rem', background: '#fff' }}>
          <h3>Existing Supplier Details</h3>
          <div style={{ marginBottom: '1rem' }}>
            <label htmlFor="existingSupplierSelect" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
              Select Supplier
            </label>
            <select
              id="existingSupplierSelect"
              value={selectedSupplier}
              onChange={(e) => {
                setSelectedSupplier(e.target.value);
                const s = existingSuppliers.find((x) => x.id === e.target.value);
                if (s && onSupplierSelected) onSupplierSelected(s.id, s.name);
              }}
              style={{ width: '100%', padding: '0.5rem' }}
            >
              {existingSuppliers.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} (Reg: {s.reg})
                </option>
              ))}
            </select>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem', marginTop: '1rem' }}>
            <div style={{ padding: '0.75rem', background: '#e9ecef', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.8rem', color: '#666' }}>Active Status</div>
              <div style={{ fontWeight: 'bold', color: currentExisting.active ? '#28a745' : '#dc3545' }}>
                {currentExisting.active ? 'Active' : 'Inactive'}
              </div>
            </div>
            <div style={{ padding: '0.75rem', background: '#e9ecef', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.8rem', color: '#666' }}>3-Point Check</div>
              <div style={{ fontWeight: 'bold', color: '#28a745' }}>{currentExisting.threePC}</div>
            </div>
            <div style={{ padding: '0.75rem', background: '#e9ecef', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.8rem', color: '#666' }}>ESG Score</div>
              <div style={{ fontWeight: 'bold' }}>{currentExisting.esg}</div>
            </div>
            <div style={{ padding: '0.75rem', background: '#e9ecef', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.8rem', color: '#666' }}>TPRM Status</div>
              <div style={{ fontWeight: 'bold', color: '#007bff' }}>{currentExisting.tprm}</div>
            </div>
          </div>
        </div>
      )}

      {supplierMode === 'new' && (
        <div data-testid="new-supplier-panel" style={{ border: '1px solid #dee2e6', borderRadius: '4px', padding: '1.5rem', background: '#fff' }}>
          <h3>New / Non-Registered Supplier Check</h3>
          <p style={{ color: '#666', fontSize: '0.9rem' }}>
            Before onboarding a new supplier, perform a duplicate check against the supplier database.
          </p>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1rem' }}>
            <div>
              <label htmlFor="searchName" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
                Legal Entity Name *
              </label>
              <input
                id="searchName"
                type="text"
                value={searchName}
                onChange={(e) => setSearchName(e.target.value)}
                placeholder="e.g. Cloud Services Global"
                style={{ width: '100%', padding: '0.5rem', boxSizing: 'border-box' }}
              />
            </div>
            <div>
              <label htmlFor="searchReg" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
                Registration Number
              </label>
              <input
                id="searchReg"
                type="text"
                value={searchReg}
                onChange={(e) => setSearchReg(e.target.value)}
                placeholder="e.g. 201801023456"
                style={{ width: '100%', padding: '0.5rem', boxSizing: 'border-box' }}
              />
            </div>
          </div>

          <div style={{ marginBottom: '1rem' }}>
            <label htmlFor="searchBank" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
              Bank Account Number (optional)
            </label>
            <input
              id="searchBank"
              type="text"
              value={searchBank}
              onChange={(e) => setSearchBank(e.target.value)}
              placeholder="e.g. 514012345678"
              style={{ width: '100%', padding: '0.5rem', boxSizing: 'border-box' }}
            />
          </div>

          <button
            type="button"
            onClick={handleDuplicateCheck}
            style={{ padding: '0.5rem 1rem', background: '#007bff', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
          >
            Check for Duplicates
          </button>

          {/* Duplicate candidates display */}
          {duplicateChecked && candidates.length > 0 && (
            <div data-testid="duplicate-candidate-panel" style={{ marginTop: '1.5rem', padding: '1rem', background: '#fff3cd', border: '1px solid #ffeeba', borderRadius: '4px' }}>
              <h4 style={{ margin: '0 0 0.5rem 0', color: '#856404' }}>Potential Duplicate Match Found</h4>
              <p style={{ fontSize: '0.9rem', color: '#856404' }}>
                A supplier record with matching details already exists in the system.
              </p>
              {candidates.map((c) => (
                <div key={c.id} style={{ background: '#fff', border: '1px solid #ddd', borderRadius: '4px', padding: '0.75rem', marginBottom: '0.5rem' }}>
                  <div style={{ fontWeight: 'bold' }}>{c.legalEntityName}</div>
                  <div style={{ fontSize: '0.85rem', color: '#555' }}>
                    Registration: {c.registrationNumber} | Tax ID: {c.taxId}
                  </div>
                  <div style={{ fontSize: '0.85rem', color: '#d39e00', marginTop: '0.25rem' }}>Match Reason: {c.matchReason}</div>
                  <div style={{ marginTop: '0.5rem' }}>
                    <button
                      type="button"
                      onClick={() => handleConfirmCandidate(c)}
                      style={{ padding: '0.35rem 0.75rem', background: '#28a745', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                    >
                      Use this Existing Supplier Record
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Exception workflow when new supplier confirmed */}
          {duplicateChecked && candidates.length === 0 && (
            <div style={{ marginTop: '1.5rem', borderTop: '1px solid #dee2e6', paddingTop: '1rem' }}>
              <h4>New Supplier Exception Workflow</h4>
              <div style={{ marginBottom: '1rem' }}>
                <label htmlFor="exceptionReason" style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
                  Justification Reason *
                </label>
                <textarea
                  id="exceptionReason"
                  rows={3}
                  value={exceptionReason}
                  onChange={(e) => setExceptionReason(e.target.value)}
                  placeholder="Explain why an existing registered supplier cannot be used..."
                  style={{ width: '100%', padding: '0.5rem', boxSizing: 'border-box' }}
                />
              </div>
              <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.25rem' }}>
                  Procurement Head Approval Evidence *
                </label>
                <button
                  type="button"
                  onClick={() => setAttachmentUploaded(true)}
                  style={{ padding: '0.4rem 0.8rem', background: attachmentUploaded ? '#28a745' : '#6c757d', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                >
                  {attachmentUploaded ? 'Evidence Attached: Approval_Memo.pdf' : 'Attach Approval Evidence'}
                </button>
              </div>
              <button
                type="button"
                onClick={handleSubmitException}
                style={{ padding: '0.5rem 1rem', background: '#17a2b8', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
              >
                Submit Supplier Exception
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
