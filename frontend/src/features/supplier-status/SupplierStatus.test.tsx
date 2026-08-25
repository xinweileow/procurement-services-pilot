import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SupplierStatus } from './SupplierStatus';

describe('SupplierStatus', () => {
  it('renders existing supplier panel by default with status metrics', () => {
    render(<SupplierStatus />);

    expect(screen.getByRole('heading', { name: /Supplier Identification & Status/i })).toBeInTheDocument();
    expect(screen.getByTestId('existing-supplier-panel')).toBeInTheDocument();
    expect(screen.getByText(/Cloud Services Global Sdn Bhd/i)).toBeInTheDocument();
    expect(screen.getByText(/3-Point Check/i)).toBeInTheDocument();
  });

  it('switches to new supplier mode, performs duplicate check, and displays candidate with confirmation prompt', async () => {
    render(<SupplierStatus />);

    // Switch to new supplier mode
    const newRadio = screen.getByLabelText(/New \/ non-registered supplier/i);
    fireEvent.click(newRadio);

    expect(screen.getByTestId('new-supplier-panel')).toBeInTheDocument();

    // Fill in duplicate-trigger name
    const nameInput = screen.getByLabelText(/Legal Entity Name \*/i);
    fireEvent.change(nameInput, { target: { value: 'Cloud Services Global' } });

    const checkButton = screen.getByRole('button', { name: /Check for Duplicates/i });
    fireEvent.click(checkButton);

    const duplicatePanel = await screen.findByTestId('duplicate-candidate-panel');
    expect(duplicatePanel).toBeInTheDocument();
    expect(screen.getByText(/Potential Duplicate Match Found/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Use this Existing Supplier Record/i })).toBeInTheDocument();
  });
});
