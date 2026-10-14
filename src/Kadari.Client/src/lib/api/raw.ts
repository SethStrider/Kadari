import { localTokenStore } from "./tokens";

/**
 * Shared helpers for the raw-`fetch` calls (multipart upload, auth) that the typed {@link ApiClient} does
 * not cover. Same base URL and bearer token as the typed client.
 */

export function apiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
  // Undefined = not configured (refuse to guess); an explicit "" = deliberate same-origin (Docker/nginx
  // proxies /api → the Web API), which must be honoured, not treated as unset. Mirrors resolveBaseUrl.
  if (configured === undefined) {
    throw new Error("VITE_API_BASE_URL is not set.");
  }
  return configured.replace(/\/$/, "");
}

export function authHeaders(): Record<string, string> {
  const headers: Record<string, string> = {
    "X-Correlation-ID": crypto.randomUUID(),
  };
  const accessToken = localTokenStore.read()?.accessToken;
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }
  return headers;
}
