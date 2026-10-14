import { Link } from "@tanstack/react-router";
import { Users, ShieldCheck, type LucideIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { useAppStore } from "@/stores/app-store";
import { useInsurerContext } from "@/lib/theme/insurer-theme";

interface NavItem {
  label: string;
  icon: LucideIcon;
  to?: string;
}

/**
 * Left navigation, ported from the now-ui-kit dashboard layout and reskinned to Trustli: a Midnight rail
 * with a brand-gradient active pill. For a white-label insurer the brand block shows the tenant's own logo
 * and name and a Portfolio entry appears; a plain staff user keeps the KADARI chrome.
 */
const items: NavItem[] = [
  { label: "Users", icon: Users, to: "/dashboard/users" },
];

/** A logo reference is only rendered as an <img> when it is a resolvable URL; otherwise the name stands in. */
function logoUrl(reference: string | null | undefined): string | null {
  if (!reference) return null;
  return /^(https?:\/\/|\/)/.test(reference) ? reference : null;
}

export function DashboardSidebar() {
  const open = useAppStore((s) => s.sidebarOpen);
  const { context, isInsurer } = useInsurerContext();

  const brandLogo = logoUrl(context?.branding?.logoReference);
  const brandName = context?.name ?? "KADARI";
  const brandTag = isInsurer ? "Insurer console" : "Admin";

  const nav: NavItem[] = isInsurer
    ? [
        { label: "Portfolio", icon: ShieldCheck, to: "/dashboard/portfolio" },
        ...items,
      ]
    : items;

  return (
    <aside
      className={cn(
        "hidden shrink-0 border-r border-white/5 bg-trustli-midnight/80 backdrop-blur-md transition-[width] duration-300 lg:block",
        open ? "w-64" : "w-20",
      )}
    >
      <div className="flex h-16 items-center gap-3 px-5">
        {brandLogo ? (
          <img
            src={brandLogo}
            alt={brandName}
            className="h-7 w-auto max-w-[9rem] object-contain"
          />
        ) : isInsurer ? (
          // Branded insurer without a logo: show their name as a wordmark, not the KADARI wordmark.
          <span className="truncate font-heading text-lg font-bold text-off-white">
            {brandName}
          </span>
        ) : (
          <span className="font-heading text-lg font-semibold tracking-[0.22em] text-off-white">
            KADARI
          </span>
        )}
        {open && (
          <span className="font-technical text-[10px] tracking-[0.28em] text-mid-grey uppercase">
            {brandTag}
          </span>
        )}
      </div>

      <nav className="flex flex-col gap-1 px-3 py-4">
        {nav.map(({ label, icon: Icon, to }) =>
          to ? (
            <Link
              key={label}
              to={to}
              activeOptions={{ exact: true }}
              className="group flex items-center gap-3 rounded-xl px-3 py-2.5 font-heading text-sm font-medium text-soft-grey transition-colors hover:bg-white/5 hover:text-off-white [&.active]:bg-gradient-to-r [&.active]:from-[#00DCC2]/20 [&.active]:to-[#095BFF]/20 [&.active]:text-off-white"
            >
              <Icon className="size-5 shrink-0" />
              {open && <span>{label}</span>}
            </Link>
          ) : (
            <span
              key={label}
              className="flex cursor-not-allowed items-center gap-3 rounded-xl px-3 py-2.5 font-heading text-sm font-medium text-mid-grey/70"
              title="Coming soon"
            >
              <Icon className="size-5 shrink-0" />
              {open && (
                <span className="flex items-center gap-2">
                  {label}
                  <span className="rounded-full bg-white/5 px-1.5 py-0.5 font-technical text-[9px] tracking-wide text-mid-grey uppercase">
                    soon
                  </span>
                </span>
              )}
            </span>
          ),
        )}
      </nav>
    </aside>
  );
}
