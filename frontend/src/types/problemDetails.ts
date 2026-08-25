/** RFC 7807 ProblemDetails shape — every non-2xx backend response uses this (docs/kb/technical_kb.md Conventions). */
export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    'title' in value &&
    'status' in value
  );
}
