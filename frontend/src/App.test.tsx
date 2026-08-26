import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import App from './App';

describe('App', () => {
  it('renders the dashboard by default inside the Etiqa Procurement shell', () => {
    render(<App />);
    expect(screen.getByRole('heading', { name: /^Procurement$/i })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { name: /Dashboard/i }).length).toBeGreaterThan(0);
  });
});
