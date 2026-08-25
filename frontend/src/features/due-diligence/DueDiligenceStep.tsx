import { useState } from 'react';
import { Card, PageHeader, StatusMessage, Badge, inputClass, labelClass } from '../../components/ui';

export interface DueDiligenceStepProps {
  initialSourcingType?: 'Sourceable' | 'Non-Sourceable';
  initialSpendType?: 'Addressable Spend' | 'Non-Addressable Spend';
  onApplicabilityChange?: (sourcingType: string, spendType: string) => void;
}

interface TileProps {
  testId?: string;
  reqTestId?: string;
  readyTestId?: string;
  title: string;
  subtitle: string;
  required: boolean;
  status: string;
}

function AssessmentTile({ testId, reqTestId, readyTestId, title, subtitle, required, status }: TileProps) {
  return (
    <div data-testid={testId} className="bg-surface border border-line rounded-lg p-4">
      <div className="font-semibold text-sm text-ink">{title}</div>
      <div className="text-2xs text-ink-muted mt-0.5">{subtitle}</div>
      <div className="mt-3 flex flex-wrap gap-1.5">
        <span data-testid={reqTestId}>
          <Badge tone={required ? 'info' : 'neutral'}>{required ? 'Required' : 'Not applicable'}</Badge>
        </span>
        <span data-testid={readyTestId}>
          <Badge tone={status === 'Ready' ? 'success' : status === 'Review required' ? 'danger' : 'warning'}>{status}</Badge>
        </span>
      </div>
    </div>
  );
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

  const threePCRequired = isAddressable;
  const esgRequired = isSourceable && isAddressable;
  const associatedPersonRequired = isAddressable;
  const isMaterial = hasSystemAccess || hasDataAccess;

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
    <div>
      <PageHeader title="Supplier Due Diligence & Applicability Assessment" />

      <StatusMessage tone="info">
        <div className="font-semibold mb-1">Procurement Policy Guidance</div>
        <p className="font-normal">
          Due diligence requirements are determined dynamically by Sourcing Type (Sourceable vs. Non-Sourceable) and
          Spend Type (Addressable vs. Non-Addressable spend).
        </p>
      </StatusMessage>

      <Card className="mb-5">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label htmlFor="sourcingTypeSelect" className={labelClass}>
              Sourcing Type
            </label>
            <select
              id="sourcingTypeSelect"
              className={inputClass}
              value={sourcingType}
              onChange={(e) => handleSourcingTypeChange(e.target.value as 'Sourceable' | 'Non-Sourceable')}
            >
              <option value="Sourceable">Sourceable</option>
              <option value="Non-Sourceable">Non-Sourceable</option>
            </select>
          </div>

          <div>
            <label htmlFor="spendTypeSelect" className={labelClass}>
              Spend Type
            </label>
            <select
              id="spendTypeSelect"
              className={inputClass}
              value={spendType}
              onChange={(e) => handleSpendTypeChange(e.target.value as 'Addressable Spend' | 'Non-Addressable Spend')}
            >
              <option value="Addressable Spend">Addressable Spend</option>
              <option value="Non-Addressable Spend">Non-Addressable Spend</option>
            </select>
          </div>
        </div>
      </Card>

      <div data-testid="applicable-rule-callout" className="mb-5 px-4 py-3 bg-canvas-subtle border-l-4 border-info rounded-r-md">
        <strong className="text-ink">Applicable Rule:</strong> <span className="text-ink-muted">{getRuleText()}</span>
      </div>

      <Card title="Materiality & System/Data Access Flags" className="mb-5">
        <div className="flex flex-wrap gap-6">
          <label className="text-sm text-ink inline-flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={hasSystemAccess} onChange={(e) => setHasSystemAccess(e.target.checked)} />
            Supplier requires access to Etiqa Internal IT Systems
          </label>
          <label className="text-sm text-ink inline-flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={hasDataAccess} onChange={(e) => setHasDataAccess(e.target.checked)} />
            Supplier processes Customer / Sensitive Personal Data
          </label>
        </div>
        <div className="mt-3 text-2xs text-ink-muted">
          Materiality Status:{' '}
          <strong className={isMaterial ? 'text-danger' : 'text-success'}>
            {isMaterial ? 'Material Supplier (Enhanced TPRM required)' : 'Non-Material'}
          </strong>
        </div>
      </Card>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
        <AssessmentTile
          testId="tile-3pc"
          reqTestId="badge-3pc-req"
          readyTestId="badge-3pc-ready"
          title="3-Point Check"
          subtitle="Identity & Registration"
          required={threePCRequired}
          status={threePCStatus}
        />
        <AssessmentTile
          testId="tile-esg"
          reqTestId="badge-esg-req"
          readyTestId="badge-esg-ready"
          title="ESG Assessment"
          subtitle="Sustainability & Governance"
          required={esgRequired}
          status={esgStatus}
        />
        <AssessmentTile
          testId="tile-associated-person"
          title="Associated Person"
          subtitle="Anti-Bribery S.17A Check"
          required={associatedPersonRequired}
          status={associatedPersonStatus}
        />
        <AssessmentTile testId="tile-tprm" title="TPRM Review" subtitle="Third-Party Risk Review" required={isMaterial} status={tprmStatus} />
      </div>

      <StatusMessage tone={isReady ? 'success' : 'warning'}>
        <strong>Overall Due Diligence Status:</strong>{' '}
        {isReady
          ? 'Ready — All applicable due diligence checks and evidence verified.'
          : 'Review Required — Mandatory checks or material supplier documentation pending.'}
      </StatusMessage>
    </div>
  );
}
