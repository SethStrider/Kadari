import { useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import {
  Boxes,
  Users,
  Wallet,
  ShieldCheck,
  ChevronLeft,
  ChevronRight,
  type LucideIcon,
} from "lucide-react";

import { DashboardNavbar } from "@/components/dashboard/dashboard-navbar";
import { useInsurerContext } from "@/lib/theme/insurer-theme";
import { getTenantPortfolio, listTenantAssets } from "@/lib/api/insurer";

export const Route = createFileRoute("/dashboard/portfolio")({
  component: InsurerPortfolio,
});

const PAGE_SIZE = 10;

// The API types integers as `string | number` (int64-safe JSON), so numeric fields are coerced here.
function num(v: string | number | null | undefined): number | null {
  if (v === null || v === undefined) return null;
  const n = typeof v === "string" ? Number(v) : v;
  return Number.isFinite(n) ? n : null;
}

function money(
  minor: string | number | null | undefined,
  currency: string | null | undefined,
): string {
  const value = num(minor);
  if (value === null) return "—";
  try {
    return new Intl.NumberFormat("en-ZA", {
      style: "currency",
      currency: currency ?? "ZAR",
      maximumFractionDigits: 0,
    }).format(value / 100);
  } catch {
    return `${currency ?? "ZAR"} ${(value / 100).toLocaleString()}`;
  }
}

function StatTile({
  label,
  value,
  sub,
  icon: Icon,
}: {
  label: string;
  value: string;
  sub?: string;
  icon: LucideIcon;
}) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5 backdrop-blur-md">
      <div className="flex items-start justify-between">
        <div className="flex flex-col gap-1">
          <span className="font-technical text-[11px] tracking-wide text-mid-grey uppercase">
            {label}
          </span>
          <span className="font-heading text-2xl font-bold text-off-white">
            {value}
          </span>
          {sub && (
            <span className="font-body text-xs text-mid-grey">{sub}</span>
          )}
        </div>
        <div className="flex size-11 items-center justify-center rounded-xl bg-primary/15 text-accent">
          <Icon className="size-5" />
        </div>
      </div>
    </div>
  );
}

function Panel({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-2xl border border-white/10 bg-white/[0.03] p-6 backdrop-blur-md">
      <h3 className="font-heading text-base font-semibold text-off-white">
        {title}
      </h3>
      <div className="mt-5">{children}</div>
    </section>
  );
}

function InsurerPortfolio() {
  const { context, isInsurer, isLoading: contextLoading } = useInsurerContext();
  const tenantId = context?.tenantId;
  const [page, setPage] = useState(1);

  const portfolio = useQuery({
    queryKey: ["tenant-portfolio", tenantId],
    queryFn: () => getTenantPortfolio(tenantId!),
    enabled: !!tenantId,
  });

  const assets = useQuery({
    queryKey: ["tenant-assets", tenantId, page],
    queryFn: () => listTenantAssets(tenantId!, page, PAGE_SIZE),
    enabled: !!tenantId,
    placeholderData: keepPreviousData,
  });

  if (contextLoading) {
    return (
      <>
        <DashboardNavbar title="Portfolio" />
        <main className="flex-1 p-6 lg:p-8">
          <p className="font-body text-sm text-soft-grey">Loading console…</p>
        </main>
      </>
    );
  }

  if (!isInsurer) {
    return (
      <>
        <DashboardNavbar title="Portfolio" />
        <main className="flex-1 p-6 lg:p-8">
          <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-8 text-center backdrop-blur-md">
            <ShieldCheck className="mx-auto size-8 text-mid-grey" />
            <h2 className="mt-3 font-heading text-lg font-semibold text-off-white">
              Insurer console
            </h2>
            <p className="mx-auto mt-1 max-w-md font-body text-sm text-soft-grey">
              This view is available to insurer accounts bound to a tenant. Your
              account has no insurer tenant, so there is no cohort to show.
            </p>
          </div>
        </main>
      </>
    );
  }

  const p = portfolio.data;
  const rows = assets.data?.items ?? [];
  const total = num(assets.data?.totalCount) ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const maxAssurance = Math.max(
    1,
    ...(p?.assuranceBreakdown ?? []).map((b) => num(b.count) ?? 0),
  );

  return (
    <>
      <DashboardNavbar title="Portfolio" />

      <main className="flex-1 overflow-y-auto p-6 lg:p-8">
        <div className="mb-8">
          <h2 className="font-heading text-2xl font-bold text-off-white">
            {context?.name} — asset portfolio
          </h2>
          <p className="mt-1 font-body text-sm text-soft-grey">
            The whole-cohort view across this tenant's policyholders: exposure,
            how well-evidenced the book is, and where the value sits.
          </p>
        </div>

        {portfolio.isError ? (
          <div className="rounded-2xl border border-destructive/30 bg-destructive/10 p-5 font-body text-sm text-off-white">
            Could not load the portfolio. You may not have permission for this
            tenant.
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
              <StatTile
                label="Assets"
                value={p ? p.assetCount.toLocaleString() : "—"}
                icon={Boxes}
              />
              <StatTile
                label="Policyholders"
                value={p ? p.policyholderCount.toLocaleString() : "—"}
                icon={Users}
              />
              <StatTile
                label="Recorded exposure"
                value={
                  p
                    ? money(p.recordedReplacementExposureMinor, p.currency)
                    : "—"
                }
                sub="Best recorded replacement value"
                icon={Wallet}
              />
              <StatTile
                label="Evidenced"
                value={p ? `${p.completenessPercent}%` : "—"}
                sub="Assets with a replacement value"
                icon={ShieldCheck}
              />
            </div>

            <div className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-2">
              <Panel title="Assurance distribution">
                <ul className="flex flex-col gap-3">
                  {(p?.assuranceBreakdown ?? []).map((band) => (
                    <li key={band.assurance} className="flex flex-col gap-1.5">
                      <div className="flex items-center justify-between font-body text-xs text-soft-grey">
                        <span>{band.assurance}</span>
                        <span className="font-heading font-semibold text-off-white">
                          {band.count}
                        </span>
                      </div>
                      <div className="h-1.5 overflow-hidden rounded-full bg-white/5">
                        <div
                          className="h-full rounded-full bg-accent"
                          style={{
                            width: `${((num(band.count) ?? 0) / maxAssurance) * 100}%`,
                          }}
                        />
                      </div>
                    </li>
                  ))}
                </ul>
              </Panel>

              <Panel title="By category">
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[320px] text-left">
                    <thead>
                      <tr className="font-technical text-[11px] tracking-wide text-mid-grey uppercase">
                        <th className="pb-2 font-medium">Category</th>
                        <th className="pb-2 text-right font-medium">Count</th>
                        <th className="pb-2 text-right font-medium">
                          Exposure
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {(p?.categories ?? []).map((c) => (
                        <tr
                          key={c.category}
                          className="border-t border-white/5 font-body text-sm"
                        >
                          <td className="py-2.5 text-off-white">
                            {c.category}
                          </td>
                          <td className="py-2.5 text-right text-soft-grey">
                            {c.count}
                          </td>
                          <td className="py-2.5 text-right text-soft-grey">
                            {money(c.exposureMinor, p?.currency)}
                          </td>
                        </tr>
                      ))}
                      {(p?.categories?.length ?? 0) === 0 && (
                        <tr>
                          <td
                            colSpan={3}
                            className="py-4 text-center font-body text-sm text-mid-grey"
                          >
                            No assets yet.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </Panel>
            </div>

            <section className="mt-6 rounded-2xl border border-white/10 bg-white/[0.03] backdrop-blur-md">
              <div className="flex items-center justify-between border-b border-white/5 px-6 py-4">
                <h3 className="font-heading text-base font-semibold text-off-white">
                  Assets
                </h3>
                <span className="font-technical text-[11px] tracking-wide text-mid-grey uppercase">
                  {total.toLocaleString()} total
                </span>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-left">
                  <thead>
                    <tr className="font-technical text-[11px] tracking-wide text-mid-grey uppercase">
                      <th className="px-6 py-3 font-medium">Asset</th>
                      <th className="px-6 py-3 font-medium">Category</th>
                      <th className="px-6 py-3 font-medium">Assurance</th>
                      <th className="px-6 py-3 font-medium">Status</th>
                      <th className="px-6 py-3 text-right font-medium">
                        Value
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((a) => (
                      <tr
                        key={a.assetId}
                        className="border-t border-white/5 font-body text-sm"
                      >
                        <td className="px-6 py-3.5 text-off-white">
                          {a.displayName}
                          {a.roomLabel && (
                            <span className="ml-2 text-xs text-mid-grey">
                              {a.roomLabel}
                            </span>
                          )}
                        </td>
                        <td className="px-6 py-3.5 text-soft-grey">
                          {a.category}
                        </td>
                        <td className="px-6 py-3.5">
                          <span className="rounded-full border border-white/10 bg-white/5 px-2.5 py-1 font-technical text-[10px] tracking-wide text-soft-grey uppercase">
                            {a.assurance}
                          </span>
                        </td>
                        <td className="px-6 py-3.5 text-soft-grey">
                          {a.status}
                        </td>
                        <td className="px-6 py-3.5 text-right text-soft-grey">
                          {money(a.bestValueMinor, a.currency)}
                        </td>
                      </tr>
                    ))}
                    {rows.length === 0 && !assets.isLoading && (
                      <tr>
                        <td
                          colSpan={5}
                          className="px-6 py-6 text-center font-body text-sm text-mid-grey"
                        >
                          No assets in this cohort yet.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div className="flex items-center justify-between border-t border-white/5 px-6 py-3">
                <span className="font-body text-xs text-mid-grey">
                  Page {page} of {totalPages}
                </span>
                <div className="flex gap-2">
                  <button
                    type="button"
                    disabled={page <= 1}
                    onClick={() => setPage((n) => Math.max(1, n - 1))}
                    className="flex items-center gap-1 rounded-lg border border-white/10 px-3 py-1.5 font-body text-xs text-soft-grey transition-colors enabled:hover:bg-white/5 disabled:opacity-40"
                  >
                    <ChevronLeft className="size-4" /> Prev
                  </button>
                  <button
                    type="button"
                    disabled={page >= totalPages}
                    onClick={() => setPage((n) => n + 1)}
                    className="flex items-center gap-1 rounded-lg border border-white/10 px-3 py-1.5 font-body text-xs text-soft-grey transition-colors enabled:hover:bg-white/5 disabled:opacity-40"
                  >
                    Next <ChevronRight className="size-4" />
                  </button>
                </div>
              </div>
            </section>
          </>
        )}
      </main>
    </>
  );
}
