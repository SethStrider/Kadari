import type { CSSProperties, ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { Camera, ChartColumn, type LucideIcon } from "lucide-react";

import { KadariLogo } from "@/components/kadari/kadari-logo";

import { getBranding } from "@/lib/api/kadari";


/** The KADARI surface: scopes the KADARI tokens, applies the customer's insurer white-label (if any), then
 *  lays out the brand sidebar (a top bar on phones) + page body, as on the brand sheet. */
export function KadariShell({
  customerId,
  title,
  subtitle,
  action,
  children,
}: {
  /** Omit on public pages (no signed-in customer) — then no insurer white-label is applied. */
  customerId?: string;
  title: string;
  subtitle?: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  const { data } = useQuery({
    queryKey: ["kadari", "branding", customerId],
    queryFn: ({ signal }) => getBranding(customerId!, signal),
    enabled: !!customerId,
    staleTime: 5 * 60_000,
    retry: false, // the default KADARI look is a fine fallback — don't hammer the endpoint
  });
  const branding = data?.branding ?? null;
  // Same rule as the Admin console: only a URL renders; a bare blob key has no public address yet.
  const logo =
    branding?.logoReference && /^(https?:\/\/|\/)/.test(branding.logoReference)
      ? branding.logoReference
      : null;

  const brandStyle = branding
    ? ({
        "--tenant-primary": branding.primaryColor,
        "--tenant-accent": branding.accentColor,
        "--tenant-on-primary": readableOn(branding.primaryColor),
      } as CSSProperties)
    : undefined;

  return (
    <div
      className="kadari flex min-h-svh flex-1 flex-col lg:flex-row"
      data-brand={branding ? "tenant" : undefined}
      style={brandStyle}
    >
      {/* Always dark, in both themes — the brand sheet's navy panel. */}
      <aside className="flex flex-col gap-(--space-5) border-(--kd-neutral-800) bg-(--kd-neutral-950) px-(--space-5) py-(--space-4) text-(--kd-neutral-25) lg:sticky lg:top-0 lg:h-svh lg:w-64 lg:shrink-0 lg:border-r lg:px-(--space-6) lg:py-(--space-7)">
        <div className="flex items-center justify-between gap-(--space-4) lg:flex-col lg:items-start">
          <KadariLogo />
          {data?.insurerName && branding && (
            <span className="flex items-center gap-(--space-2) text-xs text-(--kd-neutral-300)">
              with
              {logo ? (
                <span className="rounded-(--radius-sm) bg-white px-(--space-2) py-(--space-1)">
                  <img
                    src={logo}
                    alt={data.insurerName}
                    className="h-(--icon-size-md) w-auto"
                  />
                </span>
              ) : (
                <span className="font-medium text-(--kd-neutral-25)">
                  {data.insurerName}
                </span>
              )}
            </span>
          )}
        </div>

        {customerId && (
          <nav
            aria-label="KADARI"
            className="-mx-(--space-2) flex gap-(--space-2) overflow-x-auto lg:mx-0 lg:mt-(--space-5) lg:flex-col"
          >
            <NavItem
              to="/$customerId/ledger"
              customerId={customerId}
              Icon={ChartColumn}
              label="Asset Position"
            />
            <NavItem
              to="/$customerId/capture"
              customerId={customerId}
              Icon={Camera}
              label="Capture"
            />
          </nav>
        )}

        <div className="mt-auto hidden lg:block">
          <span className="block h-0.5 w-10 bg-(--color-accent)" />
          <p className="mt-(--space-4) text-sm text-(--kd-neutral-300)">
            Every asset. Accounted for.
          </p>
        </div>
      </aside>

      <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-(--space-7) px-(--space-5) py-(--space-8) lg:px-(--space-8)">
        <div className="flex items-start justify-between gap-(--space-5)">
          <div>
            <h1 className="text-[length:var(--text-h1-size)] leading-(--text-h1-line) font-semibold tracking-[-0.02em]">
              {title}
            </h1>
            <span className="mt-(--space-3) block h-0.5 w-10 bg-(--color-accent)" />
            {subtitle && (
              <p className="mt-(--space-3) text-sm text-(--color-text-muted)">
                {subtitle}
              </p>
            )}
          </div>
          {action}
        </div>
        {children}
      </main>
    </div>
  );
}

function NavItem({
  to,
  customerId,
  Icon,
  label,
}: {
  to: "/$customerId/ledger" | "/$customerId/capture";
  customerId: string;
  Icon: LucideIcon;
  label: string;
}) {
  return (
    <Link
      to={to}
      params={{ customerId }}
      className="kd-focus flex shrink-0 items-center gap-(--space-3) rounded-(--radius-md) border-l-2 border-transparent px-(--space-4) py-(--space-3) text-sm font-medium text-(--kd-neutral-300) transition-colors hover:bg-(--kd-neutral-900) hover:text-(--kd-neutral-25)"
      activeProps={{
        className:
          "!border-(--color-accent) bg-(--kd-neutral-850) !text-(--kd-neutral-25)",
      }}
    >
      <Icon className="size-4" aria-hidden />
      {label}
    </Link>
  );
}

/** White or near-black — whichever reads better on the insurer's primary (WCAG relative luminance). */
function readableOn(hex: string): string {
  const channel = (i: number) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  const luminance =
    0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
  const onWhite = 1.05 / (luminance + 0.05);
  const onDark = (luminance + 0.05) / 0.05;
  return onWhite >= onDark ? "#FFFFFF" : "#001920";
}
