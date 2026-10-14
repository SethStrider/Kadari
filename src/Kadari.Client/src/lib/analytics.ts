/**
 * Lightweight analytics (FE-001 "analytics fire"). Events are pushed to a GTM-style `dataLayer` queue so a
 * tag manager / product-analytics provider can be wired in production without touching call sites; in dev
 * they're logged. One `track()` so every CTA reports consistently.
 */
export function track(name: string, props: Record<string, unknown> = {}): void {
  const w = window as unknown as { dataLayer?: Record<string, unknown>[] };
  (w.dataLayer ??= []).push({ event: name, ...props });
  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.debug("[analytics]", name, props);
  }
}
