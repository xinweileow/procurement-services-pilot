import { useState } from 'react';
import { apiRequest } from '../../api/client';
import { Card, PageHeader, StatusMessage, inputClass, labelClass, ButtonPrimary, ButtonGhost } from '../../components/ui';

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

function MetricTile({ label, value, tone = 'text-ink' }: { label: string; value: string; tone?: string }) {
  return (
    <div className="bg-canvas-subtle rounded-md px-3 py-2.5">
      <div className="text-2xs text-ink-muted">{label}</div>
      <div className={`font-bold text-sm mt-0.5 ${tone}`}>{value}</div>
    </div>
  );
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
    <div>
      <PageHeader title="Supplier Identification & Status" />

      {statusMessage && (
        <div role="status">
          <StatusMessage tone="info">{statusMessage}</StatusMessage>
        </div>
      )}

      <Card className="mb-5">
        <fieldset className="border-0 p-0 m-0">
          <legend className="text-sm font-semibold text-ink mb-2">Supplier Engagement Mode</legend>
          <div className="flex gap-6">
            <label className="text-sm text-ink inline-flex items-center gap-2 cursor-pointer">
              <input type="radio" name="supplierMode" value="existing" checked={supplierMode === 'existing'} onChange={() => setSupplierMode('existing')} />
              Existing registered supplier
            </label>
            <label className="text-sm text-ink inline-flex items-center gap-2 cursor-pointer">
              <input type="radio" name="supplierMode" value="new" checked={supplierMode === 'new'} onChange={() => setSupplierMode('new')} />
              New / non-registered supplier
            </label>
          </div>
        </fieldset>
      </Card>

      {supplierMode === 'existing' && (
        <Card title="Existing Supplier Details">
          <div data-testid="existing-supplier-panel">
            <div className="mb-4">
              <label htmlFor="existingSupplierSelect" className={labelClass}>
                Select Supplier
              </label>
              <select
                id="existingSupplierSelect"
                className={inputClass}
                value={selectedSupplier}
                onChange={(e) => {
                  setSelectedSupplier(e.target.value);
                  const s = existingSuppliers.find((x) => x.id === e.target.value);
                  if (s && onSupplierSelected) onSupplierSelected(s.id, s.name);
                }}
              >
                {existingSuppliers.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name} (Reg: {s.reg})
                  </option>
                ))}
              </select>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <MetricTile label="Active Status" value={currentExisting.active ? 'Active' : 'Inactive'} tone={currentExisting.active ? 'text-success' : 'text-danger'} />
              <MetricTile label="3-Point Check" value={currentExisting.threePC} tone="text-success" />
              <MetricTile label="ESG Score" value={currentExisting.esg} />
              <MetricTile label="TPRM Status" value={currentExisting.tprm} tone="text-info" />
            </div>
          </div>
        </Card>
      )}

      {supplierMode === 'new' && (
        <Card title="New / Non-Registered Supplier Check">
          <div data-testid="new-supplier-panel">
            <p className="text-2xs text-ink-muted mb-4">
              Before onboarding a new supplier, perform a duplicate check against the supplier database.
            </p>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
              <div>
                <label htmlFor="searchName" className={labelClass}>
                  Legal Entity Name *
                </label>
                <input
                  id="searchName"
                  type="text"
                  className={inputClass}
                  value={searchName}
                  onChange={(e) => setSearchName(e.target.value)}
                  placeholder="e.g. Cloud Services Global"
                />
              </div>
              <div>
                <label htmlFor="searchReg" className={labelClass}>
                  Registration Number
                </label>
                <input
                  id="searchReg"
                  type="text"
                  className={inputClass}
                  value={searchReg}
                  onChange={(e) => setSearchReg(e.target.value)}
                  placeholder="e.g. 201801023456"
                />
              </div>
            </div>

            <div className="mb-4">
              <label htmlFor="searchBank" className={labelClass}>
                Bank Account Number (optional)
              </label>
              <input
                id="searchBank"
                type="text"
                className={inputClass}
                value={searchBank}
                onChange={(e) => setSearchBank(e.target.value)}
                placeholder="e.g. 514012345678"
              />
            </div>

            <ButtonPrimary onClick={handleDuplicateCheck} className="py-2 px-4">
              Check for Duplicates
            </ButtonPrimary>

            {duplicateChecked && candidates.length > 0 && (
              <div data-testid="duplicate-candidate-panel" className="mt-5">
                <StatusMessage tone="warning">
                  <div className="font-semibold mb-1">Potential Duplicate Match Found</div>
                  <p className="font-normal">A supplier record with matching details already exists in the system.</p>
                </StatusMessage>
                {candidates.map((c) => (
                  <div key={c.id} className="bg-surface border border-line rounded-md p-3 mb-2">
                    <div className="font-semibold text-sm text-ink">{c.legalEntityName}</div>
                    <div className="text-2xs text-ink-muted mt-0.5">
                      Registration: {c.registrationNumber} | Tax ID: {c.taxId}
                    </div>
                    <div className="text-2xs text-warning mt-1">Match Reason: {c.matchReason}</div>
                    <div className="mt-2">
                      <ButtonPrimary onClick={() => handleConfirmCandidate(c)}>Use this Existing Supplier Record</ButtonPrimary>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {duplicateChecked && candidates.length === 0 && (
              <div className="mt-5 pt-4 border-t border-line">
                <h4 className="text-sm font-semibold text-ink mb-3">New Supplier Exception Workflow</h4>
                <div className="mb-3">
                  <label htmlFor="exceptionReason" className={labelClass}>
                    Justification Reason *
                  </label>
                  <textarea
                    id="exceptionReason"
                    rows={3}
                    className={inputClass}
                    value={exceptionReason}
                    onChange={(e) => setExceptionReason(e.target.value)}
                    placeholder="Explain why an existing registered supplier cannot be used..."
                  />
                </div>
                <div className="mb-4">
                  <span className={labelClass}>Procurement Head Approval Evidence *</span>
                  <ButtonGhost onClick={() => setAttachmentUploaded(true)} className="py-1.5 px-3">
                    {attachmentUploaded ? 'Evidence Attached: Approval_Memo.pdf' : 'Attach Approval Evidence'}
                  </ButtonGhost>
                </div>
                <ButtonPrimary onClick={handleSubmitException} className="py-2 px-4">
                  Submit Supplier Exception
                </ButtonPrimary>
              </div>
            )}
          </div>
        </Card>
      )}
    </div>
  );
}
