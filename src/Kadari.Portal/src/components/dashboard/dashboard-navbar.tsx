import { useNavigate } from "@tanstack/react-router";
import { Menu, LogOut } from "lucide-react";

import { logout } from "@/lib/auth/login";
import { useSession } from "@/lib/auth/session";
import { useAppStore } from "@/stores/app-store";

/**
 * Top bar: sidebar toggle, page title, and the signed-in staff member with a sign-out action. Ported
 * from the now-ui-kit navbar, reskinned to the Trustli Midnight chrome.
 */
export function DashboardNavbar({ title }: { title: string }) {
  const navigate = useNavigate();
  const toggleSidebar = useAppStore((s) => s.toggleSidebar);
  const user = useSession((s) => s.user);

  const initials =
    user?.name
      ?.split(" ")
      .map((p) => p[0])
      .filter(Boolean)
      .slice(0, 2)
      .join("")
      .toUpperCase() || "TR";

  const onSignOut = async () => {
    logout();
    await navigate({ to: "/login" });
  };

  return (
    <header className="flex h-16 items-center justify-between gap-4 border-b border-white/5 bg-trustli-midnight/70 px-6 backdrop-blur-md">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={toggleSidebar}
          className="hidden rounded-lg p-2 text-soft-grey transition-colors hover:bg-white/5 hover:text-off-white lg:inline-flex"
          aria-label="Toggle sidebar"
        >
          <Menu className="size-5" />
        </button>
        <h1 className="font-heading text-lg font-semibold text-off-white">
          {title}
        </h1>
      </div>

      <div className="flex items-center gap-3">
        <div className="flex items-center gap-2.5">
          <div className="flex size-9 items-center justify-center rounded-full bg-gradient-to-br from-[#00DCC2] to-[#095BFF] font-heading text-xs font-bold text-trustli-midnight">
            {initials}
          </div>
          <div className="hidden flex-col leading-tight sm:flex">
            <span className="font-heading text-sm font-medium text-off-white">
              {user?.name || "Staff member"}
            </span>
            <span className="font-technical text-[10px] tracking-wide text-mid-grey uppercase">
              {user?.roles[0] ?? "Staff"}
            </span>
          </div>
        </div>

        <button
          type="button"
          onClick={onSignOut}
          className="inline-flex h-9 items-center gap-2 rounded-xl border border-white/10 px-3 font-heading text-sm font-medium text-soft-grey transition-colors hover:border-white/20 hover:text-off-white"
        >
          <LogOut className="size-4" /> Sign out
        </button>
      </div>
    </header>
  );
}
