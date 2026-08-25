import { useState } from 'react';
import { apiRequest } from '../../api/client';
import type { RequestResponse } from '../../types/requests';
import { Card, StatusMessage, inputClass, labelClass, ButtonPrimary } from '../../components/ui';

interface Props {
  onSubmitted: (request: RequestResponse) => void;
}

export function RequestLanding({ onSubmitted }: Props) {
  const [role, setRole] = useState('IT Business Requestor');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [department, setDepartment] = useState('');
  const [category, setCategory] = useState('IT and Telecommunication');
  const [estimatedValue, setEstimatedValue] = useState<number>(50000);
  const [deliveryDate, setDeliveryDate] = useState('2026-12-31');
  const [error, setError] = useState<string | null>(null);

  const [noConflict, setNoConflict] = useState(false);
  const [connectedParty, setConnectedParty] = useState(false);
  const [noSplitting, setNoSplitting] = useState(false);
  const [complete, setComplete] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!title.trim() || !description.trim() || !department.trim()) {
      setError('Title, business justification and department are mandatory.');
      return;
    }
    if (!(noConflict && connectedParty && noSplitting && complete)) {
      setError('Complete all four compliance declarations before submission.');
      return;
    }

    try {
      const created = await apiRequest<RequestResponse>('/requests', {
        method: 'POST',
        body: JSON.stringify({
          requesterId: 'user-current',
          requesterRole: role,
          businessUnit: department,
          costCentre: 'FIN-2040',
          category,
          estimatedValue,
          currency: 'MYR',
          title,
          description,
          department,
          country: 'Malaysia',
          entity: 'Etiqa',
          deliveryDate,
          criticality: 'Standard',
          engagementPathway: 'Sourcing with Contract',
          isNonCatalogue: true,
        }),
      });

      const submitted = await apiRequest<RequestResponse>(`/requests/${created.id}/submit`, {
        method: 'POST',
        body: JSON.stringify({
          noConflict,
          connectedPartyDeclared: connectedParty,
          noSplittingDeclaration: noSplitting,
          completeDeclaration: complete,
        }),
      });

      onSubmitted(submitted);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Submission failed');
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <Card title="Request Details & Compliance Declarations">
        {error && (
          <div role="alert" className="mb-4">
            <StatusMessage tone="danger">{error}</StatusMessage>
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
          <div>
            <label htmlFor="roleSelect" className={labelClass}>
              Login Role
            </label>
            <select
              id="roleSelect"
              className={inputClass}
              value={role}
              onChange={(e) => {
                const r = e.target.value;
                setRole(r);
                setCategory(r === 'IT Business Requestor' ? 'IT and Telecommunication' : 'General Spend');
              }}
            >
              <option>IT Business Requestor</option>
              <option>Non-IT Business Requestor</option>
              <option>Procurement Administrator</option>
            </select>
          </div>

          <div>
            <label htmlFor="categorySelect" className={labelClass}>
              Procurement Category *
            </label>
            <select id="categorySelect" className={inputClass} value={category} onChange={(e) => setCategory(e.target.value)}>
              {role === 'IT Business Requestor' ? (
                <option>IT and Telecommunication</option>
              ) : (
                <>
                  <option>General Spend</option>
                  <option>Facilities Management</option>
                  <option>Sales and Marketing</option>
                  <option>Professional Services</option>
                </>
              )}
            </select>
          </div>
        </div>

        <div className="mb-4">
          <label htmlFor="titleInput" className={labelClass}>
            Request title *
          </label>
          <input id="titleInput" className={inputClass} value={title} onChange={(e) => setTitle(e.target.value)} />
        </div>

        <div className="mb-4">
          <label htmlFor="descriptionInput" className={labelClass}>
            Business justification *
          </label>
          <textarea id="descriptionInput" rows={3} className={inputClass} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>

        <div className="mb-4">
          <label htmlFor="departmentInput" className={labelClass}>
            Department *
          </label>
          <input id="departmentInput" className={inputClass} value={department} onChange={(e) => setDepartment(e.target.value)} />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
          <div>
            <label htmlFor="valueInput" className={labelClass}>
              Estimated value (MYR) *
            </label>
            <input
              id="valueInput"
              type="number"
              className={inputClass}
              value={estimatedValue}
              onChange={(e) => setEstimatedValue(Number(e.target.value))}
            />
          </div>

          <div>
            <label htmlFor="deliveryDateInput" className={labelClass}>
              Delivery date *
            </label>
            <input
              id="deliveryDateInput"
              type="date"
              className={inputClass}
              value={deliveryDate}
              onChange={(e) => setDeliveryDate(e.target.value)}
            />
          </div>
        </div>

        <fieldset className="border-t border-line pt-4 mt-4">
          <legend className="text-sm font-semibold text-ink mb-2">Compliance Declarations</legend>
          <div className="space-y-2">
            <label className="text-sm text-ink flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={noConflict} onChange={(e) => setNoConflict(e.target.checked)} />
              No conflict of interest, or all conflicts declared
            </label>
            <label className="text-sm text-ink flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={connectedParty} onChange={(e) => setConnectedParty(e.target.checked)} />
              No undisclosed connected party transaction
            </label>
            <label className="text-sm text-ink flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={noSplitting} onChange={(e) => setNoSplitting(e.target.checked)} />
              This request is not split to avoid approval thresholds
            </label>
            <label className="text-sm text-ink flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={complete} onChange={(e) => setComplete(e.target.checked)} />
              All information is complete and accurate
            </label>
          </div>
        </fieldset>

        <div className="mt-5">
          <ButtonPrimary type="submit" className="py-2 px-4">
            Submit request
          </ButtonPrimary>
        </div>
      </Card>
    </form>
  );
}
