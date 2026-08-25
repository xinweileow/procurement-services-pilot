import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { CatalogueSearch } from './CatalogueSearch';

describe('CatalogueSearch', () => {
  it('calls onSelectNonCatalogue when "proceed as Non-Catalogue" is clicked', async () => {
    const onNonCatalogue = vi.fn();
    render(<CatalogueSearch onSelectNonCatalogue={onNonCatalogue} />);

    const button = screen.getByRole('button', { name: /proceed as non-catalogue/i });
    await userEvent.click(button);

    expect(onNonCatalogue).toHaveBeenCalledOnce();
  });
});
