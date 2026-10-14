import { createFileRoute, redirect } from "@tanstack/react-router";

import { localTokenStore } from "@/lib/api";

/**
 * Front door of the admin portal (portal.trustli.co.za). There is no public marketing surface here —
 * this is a staff tool — so the root simply routes to sign-in, or straight to the dashboard when a
 * session is already present.
 */
export const Route = createFileRoute("/")({
  beforeLoad: () => {
    throw redirect({ to: localTokenStore.read() ? "/dashboard" : "/login" });
  },
});
