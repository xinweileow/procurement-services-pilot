import { isProblemDetails, type ProblemDetails } from '../types/problemDetails';

/** Thrown for every non-2xx API response, carrying the parsed ProblemDetails. */
export class ApiError extends Error {
  problem: ProblemDetails;

  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title);
    this.problem = problem;
  }
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1';

/**
 * Shared, typed API client. Every caller in src/features/ goes through this instead of
 * hand-rolling fetch+error-handling per component — the one place that parses the backend's
 * RFC 7807 ProblemDetails error shape (docs/kb/technical_kb.md Conventions), matching
 * developer-dotnet.md's error contract on the other side of the wire.
 */
export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    if (isProblemDetails(body)) {
      throw new ApiError(body);
    }
    throw new ApiError({ title: `Request failed with status ${response.status}`, status: response.status });
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
