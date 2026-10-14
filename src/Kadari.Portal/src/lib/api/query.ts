import { ApiProblem } from "./problem";

/**
 * Shared TanStack Query helpers for reads that can fail on authorization.
 *
 * Only retry a failed read when re-issuing it could plausibly succeed (429/5xx). An authorization
 * failure (403) or any other 4xx never will, so it fails fast to the error state instead of spinning
 * through the default retries — which otherwise makes a 403 momentarily look like an empty result.
 */
export function retryOnlyTransient(
  _failureCount: number,
  error: unknown,
): boolean {
  return error instanceof ApiProblem ? error.isRetryable : false;
}

/** True when the failure is "you're not allowed here" (403) rather than "it broke" — several staff
 * views need an operational role (Verifier, QA or Customer Success) the platform administrator lacks. */
export function isForbidden(error: unknown): boolean {
  return error instanceof ApiProblem && error.status === 403;
}
