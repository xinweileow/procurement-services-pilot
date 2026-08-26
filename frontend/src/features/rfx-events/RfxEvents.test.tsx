import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { RfxEvents } from './RfxEvents';

describe('RfxEvents', () => {
  it('renders the RFx Events screen with a New RFx Event action', () => {
    render(<RfxEvents />);

    expect(screen.getByRole('heading', { name: /RFx Events/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /New RFx Event/i })).toBeInTheDocument();
  });

  it('reveals the create-draft form when "New RFx Event" is clicked', async () => {
    render(<RfxEvents />);

    await userEvent.click(screen.getByRole('button', { name: /New RFx Event/i }));

    expect(screen.getByLabelText(/Sourcing Strategy ID/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Create Draft/i })).toBeInTheDocument();
  });

  it('blocks creation without a Sourcing Strategy ID', async () => {
    render(<RfxEvents />);

    await userEvent.click(screen.getByRole('button', { name: /New RFx Event/i }));
    await userEvent.click(screen.getByRole('button', { name: /Create Draft/i }));

    expect(await screen.findByText(/Sourcing Strategy ID is required/i)).toBeInTheDocument();
  });
});
