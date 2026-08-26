import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EvaluationWorkspace } from './EvaluationWorkspace';

describe('EvaluationWorkspace', () => {
  it('renders the workspace with an RFx event selector and no scoring forms until one is picked', () => {
    render(<EvaluationWorkspace />);

    expect(screen.getByRole('heading', { name: /Evaluation Workspace/i })).toBeInTheDocument();
    expect(screen.getByText(/Select RFx Event/i)).toBeInTheDocument();
    expect(screen.queryByText(/Score Evaluation/i)).not.toBeInTheDocument();
  });
});
