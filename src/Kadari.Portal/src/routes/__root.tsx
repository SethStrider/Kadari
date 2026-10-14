import { useEffect } from "react";
import { createRootRoute, Outlet } from "@tanstack/react-router";

import { useSession } from "@/lib/auth/session";

export const Route = createRootRoute({
  component: RootLayout,
});

/**
 * Root shell for the admin portal. Deliberately thin — the public landing carries the marketing chrome
 * and the dashboard carries its own (sidebar + navbar), so the root only owns the Midnight canvas, the
 * first-paint logo intro, and session hydration from storage.
 */
function RootLayout() {
  const hydrate = useSession((s) => s.hydrate);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  return (
    <div className="min-h-dvh bg-trustli-midnight text-foreground">
      <Outlet />
    </div>
  );
}
