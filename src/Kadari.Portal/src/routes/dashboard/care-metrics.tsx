import { createFileRoute } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";

import { DashboardNavbar } from "@/components/dashboard/dashboard-navbar";
import { ApiProblem } from "@/lib/api";
import { getCareMetrics } from "@/lib/api/care-ops";
import { isForbidden, retryOnlyTransient } from "@/lib/api/query";

export const Route = createFileRoute("/dashboard/care-metrics")({
  component: CareMetricsPage,
});

// The API types integers as `string | number` (int64-safe JSON), so numeric fields are coerced here.
function num(v: string | number | null | undefined): number {
  if (v === null || v === undefined) return 0;
  const n = typeof v === "string" ? Number(v) : v;
  return Number.isFinite(n) ? n : 0;
}

const percent = (v: string | number) => `${Math.round(num(v) * 100)}%`;

/** How KADARI Care is performing (CARE/14 C14) — aggregate counts only, no customer or message content. */
function CareMetricsPage() {
  const metrics = useQuery({
    queryKey: ["care-metrics"],
    queryFn: getCareMetrics,
    retry: retryOnlyTransient,
  });

  return (
    <>
      <DashboardNavbar title="Care metrics" />
      <main className="flex flex-1 flex-col gap-6 p-6 lg:p-8">
        {metrics.isPending && (
          <p className="font-body text-sm text-soft-grey">Loading…</p>
        )}
        {metrics.isError && (
          <p className="font-body text-sm text-soft-grey">
            {isForbidden(metrics.error)
              ? "Care metrics need the System admin or Customer success role."
              : metrics.error instanceof ApiProblem
                ? metrics.error.message
                : "Something went wrong."}
          </p>
        )}
        {metrics.data && (
          <>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              <Tile
                label="Care-active assets"
                value={String(num(metrics.data.careActiveAssets))}
              />
              <Tile
                label="Coverage found"
                value={percent(metrics.data.coverageFoundRate)}
              />
              <Tile
                label="Registrations completed"
                value={`${num(metrics.data.registrationsCompleted)} / ${num(metrics.data.registrationsStarted)}`}
                sub={percent(metrics.data.registrationCompletionRate)}
              />
              <Tile
                label="Manual interventions"
                value={String(
                  num(metrics.data.manualInterventionsPer1000CareAssets),
                )}
                sub="per 1,000 Care-active assets"
              />
              <Tile
                label="Cases opened"
                value={String(num(metrics.data.casesOpened))}
                sub={`${num(metrics.data.casesOpen)} open`}
              />
              <Tile
                label="Safety-flagged cases"
                value={String(num(metrics.data.safetyFlaggedCases))}
              />
              <Tile
                label="Median resolution"
                value={
                  metrics.data.medianResolutionDays == null
                    ? "—"
                    : `${num(metrics.data.medianResolutionDays)} days`
                }
              />
              <Tile
                label="Rejection rate"
                value={percent(metrics.data.rejectionRate)}
              />
            </div>
            <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
              <Breakdown
                title="Coverage by state"
                counts={metrics.data.coverageByState}
              />
              <Breakdown
                title="Cases by route"
                counts={metrics.data.casesByRoute}
              />
              <Breakdown title="Outcomes" counts={metrics.data.outcomeMix} />
              <Breakdown
                title="Extra-cover offers"
                counts={metrics.data.opportunitiesByStatus}
              />
              <Breakdown
                title="Reminders"
                counts={metrics.data.remindersByStatus}
              />
            </div>
          </>
        )}
      </main>
    </>
  );
}

function Tile({
  label,
  value,
  sub,
}: {
  label: string;
  value: string;
  sub?: string;
}) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
      <span className="font-technical text-[11px] tracking-wide text-mid-grey uppercase">
        {label}
      </span>
      <p className="font-heading text-2xl font-bold text-off-white">{value}</p>
      {sub && <p className="font-body text-xs text-mid-grey">{sub}</p>}
    </div>
  );
}

function Breakdown({
  title,
  counts,
}: {
  title: string;
  counts: Record<string, string | number>;
}) {
  const rows = Object.entries(counts).sort((a, b) => num(b[1]) - num(a[1]));
  return (
    <section className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
      <h3 className="font-heading text-sm font-semibold text-off-white">
        {title}
      </h3>
      {rows.length === 0 ? (
        <p className="mt-3 font-body text-sm text-mid-grey">Nothing yet.</p>
      ) : (
        <dl className="mt-3 grid grid-cols-[1fr_auto] gap-x-4 gap-y-1 font-body text-sm">
          {rows.map(([k, v]) => (
            <div key={k} className="contents">
              <dt className="text-soft-grey">
                {k.replace(/([a-z])([A-Z])/g, "$1 $2")}
              </dt>
              <dd className="text-right text-off-white tabular-nums">
                {num(v)}
              </dd>
            </div>
          ))}
        </dl>
      )}
    </section>
  );
}
