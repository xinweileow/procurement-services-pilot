import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { DueDiligenceStep } from './DueDiligenceStep';

describe('DueDiligenceStep', () => {
  it('renders all 4 assessment tiles with applicability rules', () => {
    render(<DueDiligenceStep />);

    expect(screen.getByRole('heading', { name: /Supplier Due Diligence & Applicability Assessment/i })).toBeInTheDocument();
    expect(screen.getByTestId('tile-3pc')).toBeInTheDocument();
    expect(screen.getByTestId('tile-esg')).toBeInTheDocument();
    expect(screen.getByTestId('tile-associated-person')).toBeInTheDocument();
    expect(screen.getByTestId('tile-tprm')).toBeInTheDocument();
  });

  it('displays ESG as Not applicable and 3PC as Required when sourcingType is Non-Sourceable and spendType is Addressable Spend', () => {
    render(<DueDiligenceStep initialSourcingType="Non-Sourceable" initialSpendType="Addressable Spend" />);

    const badge3pcReq = screen.getByTestId('badge-3pc-req');
    expect(badge3pcReq).toHaveTextContent(/Required/i);

    const badgeEsgReq = screen.getByTestId('badge-esg-req');
    expect(badgeEsgReq).toHaveTextContent(/Not applicable/i);

    expect(screen.getByTestId('applicable-rule-callout')).toHaveTextContent(/Rule B2: Non-Sourceable Addressable Spend requires 3PC/i);
  });

  it('updates applicability dynamically when sourcingType changes', () => {
    render(<DueDiligenceStep initialSourcingType="Non-Sourceable" initialSpendType="Addressable Spend" />);

    const sourcingSelect = screen.getByLabelText(/Sourcing Type/i);
    fireEvent.change(sourcingSelect, { target: { value: 'Sourceable' } });

    const badgeEsgReq = screen.getByTestId('badge-esg-req');
    expect(badgeEsgReq).toHaveTextContent(/Required/i);
  });
});
