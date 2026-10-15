/**
 * The base URL for the anonymous raw-`fetch` auth calls (OTP request/verify). Authorized raw calls go
 * through `apiClient.fetch`, which carries the bearer token and refreshes on 401.
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
