import { createFileRoute, Outlet, redirect } from "@tanstack/react-router";

import { localTokenStore } from "@/lib/api";
import { DashboardSidebar } from "@/components/dashboard/dashboard-sidebar";
import { InsurerThemeProvider } from "@/lib/theme/insurer-theme";

export const Route = createFileRoute("/dashboard")({
  // The resource gate proper lives server-side (staff-only login + per-request checks). This is the
  // client-side convenience guard: no token, no dashboard — bounce to sign-in.
  beforeLoad: () => {
    if (!localTokenStore.read()) {
      throw redirect({ to: "/login" });
    }
  },
  component: DashboardLayout,
});

function DashboardLayout() {
  // The provider re-skins the shell to the signed-in insurer's brand (a plain staff user keeps the
  // Trustli palette), so it wraps the whole dashboard chrome — sidebar included.
  return (
    <InsurerThemeProvider>
      <div className="flex min-h-dvh bg-trustli-midnight text-foreground">
        <DashboardSidebar />
        <div className="flex min-w-0 flex-1 flex-col">
          <Outlet />
        </div>
      </div>
    </InsurerThemeProvider>
  );
}
