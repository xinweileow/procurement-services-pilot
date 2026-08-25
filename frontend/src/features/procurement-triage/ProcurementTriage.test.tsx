import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ProcurementTriage } from './ProcurementTriage';

describe('ProcurementTriage', () => {
  it('renders triage routes, governance status table, and request summary', () => {
    render(<ProcurementTriage />);

    expect(screen.getByRole('heading', { name: /Procurement Triage & Assessment Overview/i })).toBeInTheDocument();
    expect(screen.getByTestId('triage-routes')).toBeInTheDocument();
    expect(screen.getByTestId('governance-status-table')).toBeInTheDocument();
    expect(screen.getByTestId('request-summary-list')).toBeInTheDocument();
  });

  it('disables Accept for assessment action with an explanatory message when blocking issues exist', () => {
    render(<ProcurementTriage initialBlockingIssues={true} blockingReason="Unresolved KYC check on vendor." />);

    const acceptButton = screen.getByRole('button', { name: /Accept for Assessment/i });
    expect(acceptButton).toBeDisabled();

    expect(screen.getByRole('alert')).toHaveTextContent(/Unresolved KYC check on vendor/i);
    expect(screen.getByText(/Action disabled: Resolve blocking issues before accepting/i)).toBeInTheDocument();
  });

  it('enables Accept for assessment when no blocking issues exist, and displays success message on click', async () => {
    const handleSuccess = vi.fn();
    render(<ProcurementTriage initialBlockingIssues={false} onAcceptSuccess={handleSuccess} />);

    const acceptButton = screen.getByRole('button', { name: /Accept for Assessment/i });
    expect(acceptButton).toBeEnabled();

    fireEvent.click(acceptButton);

    const statusEl = await screen.findByRole('status');
    expect(statusEl).toHaveTextContent(/Request accepted and routed to Procurement Assessment/i);
    expect(handleSuccess).toHaveBeenCalled();
  });

  it('allows changing priority and notes audit trail notice', () => {
    render(<ProcurementTriage />);

    const prioritySelect = screen.getByLabelText(/Priority Level/i);
    fireEvent.change(prioritySelect, { target: { value: 'Critical' } });

    expect(prioritySelect).toHaveValue('Critical');
    expect(screen.getByText(/Priority changes are recorded in the audit trail/i)).toBeInTheDocument();
  });
});
