import { useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronRight, Loader2, ShieldPlus, Wrench } from "lucide-react";

import { CareCard } from "@/components/kadari/care-card";
import { CareCases } from "@/components/kadari/care-cases";
import { CareProductHeader } from "@/components/kadari/care-product";
import { CareReminders } from "@/components/kadari/care-reminders";
import { ExtraCover } from "@/components/kadari/extra-cover";
import { formatCareDate } from "@/components/kadari/care-format";
import { KadariShell } from "@/components/kadari/kadari-shell";
import { OwnershipProofForm } from "@/components/kadari/ownership-proof-form";
import { PurchaseDetailsForm } from "@/components/kadari/purchase-details-form";
import {
  getAssetHistory,
  getCareSummary,
  getCoverage,
  type CoverageRecord,
} from "@/lib/api/care";
import { getAsset } from "@/lib/api/kadari";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/$customerId/assets/$assetId_/care")({
  component: CarePage,
});

const tabs = ["Overview", "Problems", "Coverage", "History"] as const;
type Tab = (typeof tabs)[number];

const coverageNames: Record<string, string> = {
  Manufacturer: "Manufacturer warranty",
  RetailerGuarantee: "Retailer guarantee",
  Extended: "Extended cover",
  ServicePlan: "Service plan",
};

const stateNames: Record<string, string> = {
  Active: "Covered",
  Expiring: "Ends soon",
  Expired: "Ended",
  LikelyActive: "Likely covered",
  NeedsReview: "Being checked",
  Unknown: "Not confirmed",
  VoidOrIneligible: "Not covered",
};

const gapNames: Record<string, string> = {
  Brand: "the brand",
  PurchaseDate: "the purchase date",
  ProofOfPurchase: "a receipt or proof of purchase",
};

const dateTime = new Intl.DateTimeFormat("en-ZA", {
  dateStyle: "medium",
  timeStyle: "short",
});

/** KADARI Care for one asset (CARE/05 C02 overview, C10–C27 problems, C03 coverage, C28 history). */
function CarePage() {
  const { customerId, assetId } = Route.useParams();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>("Overview");
  // Bumped by "Something's wrong" so the Problems tab opens fresh on the report screen.
  const [reportKey, setReportKey] = useState(0);

  const asset = useQuery({
    queryKey: ["kadari", "asset", customerId, assetId],
    queryFn: ({ signal }) => getAsset(customerId, assetId, signal),
  });
  const care = useQuery({
    queryKey: ["kadari", "care", customerId, assetId],
    queryFn: ({ signal }) => getCareSummary(customerId, assetId, signal),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["kadari"] });

  const backLink = (
    <Link
      to="/$customerId/assets/$assetId"
      params={{ customerId, assetId }}
      className="kd-focus shrink-0 text-sm font-medium text-(--color-text-link) hover:underline"
    >
      ← Asset
    </Link>
  );

  const title = asset.data?.displayName ?? "KADARI Care";

  if (care.isPending || asset.isPending) {
    return (
      <KadariShell customerId={customerId} title={title} action={backLink}>
        <div className="flex items-center gap-(--space-3) text-(--color-text-muted)">
          <Loader2 className="size-4 animate-spin" /> Loading KADARI Care…
        </div>
      </KadariShell>
    );
  }

  if (care.isError || !care.data) {
    return (
      <KadariShell customerId={customerId} title={title} action={backLink}>
        <p className="text-sm text-(--color-text-muted)">
          KADARI Care isn't available for this asset yet.
        </p>
      </KadariShell>
    );
  }

  const summary = care.data;
  const product = {
    name: asset.data?.displayName ?? "This item",
    brand: summary.purchase?.brand,
    model: summary.purchase?.model,
    room: asset.data?.roomLabel,
    careCategory: summary.category,
  };

  function reportProblem() {
    setTab("Problems");
    setReportKey((k) => k + 1);
  }

  return (
    <KadariShell customerId={customerId} title="KADARI Care" action={backLink}>
      <CareProductHeader product={product} />

      <CareCard
        customerId={customerId}
        assetId={assetId}
        summary={summary}
        onChanged={() => void refresh()}
        showAction={false}
      />

      {summary.enabled && (
        <div className="flex flex-col gap-(--space-3)">
          {summary.primaryAction === "ActivateExtraCover" && (
            <ActionRow
              primary
              icon={ShieldPlus}
              label="Activate cover"
              onClick={() => {
                setTab("Overview");
                document
                  .getElementById("extra-cover")
                  ?.scrollIntoView({ behavior: "smooth" });
              }}
            />
          )}
          <ActionRow
            icon={Wrench}
            label="Something's wrong"
            onClick={reportProblem}
          />
        </div>
      )}

      <div
        role="tablist"
        aria-label="KADARI Care"
        className="flex gap-(--space-2) border-b border-(--color-border)"
      >
        {tabs.map((t) => (
          <button
            key={t}
            role="tab"
            type="button"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              "kd-focus -mb-px h-(--tap-target-min) border-b-2 px-(--space-4) text-sm font-medium",
              tab === t
                ? "border-(--color-primary) text-(--color-text)"
                : "border-transparent text-(--color-text-muted)",
            )}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === "Overview" && (
        <div className="flex flex-col gap-(--space-6)">
          <ProductDetails
            serialNumber={summary.serialNumber}
            purchasedOn={summary.purchase?.purchasedOn}
            retailer={summary.purchase?.retailer}
            coverage={
              summary.coverageState
                ? `${stateNames[summary.coverageState] ?? summary.coverageState}${
                    summary.coverageExpiresOn
                      ? ` until ${formatCareDate(summary.coverageExpiresOn)}`
                      : ""
                  }`
                : "Not worked out yet"
            }
          />
          {summary.enabled && (
            <CareReminders
              customerId={customerId}
              assetId={assetId}
              showOptIn
            />
          )}
          {summary.enabled && (
            <div id="extra-cover" className="scroll-mt-(--space-6)">
              <ExtraCover
                customerId={customerId}
                assetId={assetId}
                onChanged={() => void refresh()}
              />
            </div>
          )}
          {summary.evidenceGaps.length > 0 && (
            <p className="rounded-(--radius-md) bg-(--color-info-subtle) px-(--space-5) py-(--space-4) text-sm">
              To be sure of the dates, KADARI still needs{" "}
              {summary.evidenceGaps.map((g) => gapNames[g] ?? g).join(", ")}.
            </p>
          )}
          <Section title="Purchase details">
            <Card>
              <PurchaseDetailsForm
                key={summary.purchase?.recordedAt ?? "none"}
                customerId={customerId}
                assetId={assetId}
                initial={summary.purchase}
                onRecorded={() => void refresh()}
              />
            </Card>
          </Section>
          <Section title="Receipt or proof of purchase">
            <Card>
              <OwnershipProofForm
                customerId={customerId}
                assetId={assetId}
                onRecorded={() => void refresh()}
              />
            </Card>
          </Section>
        </div>
      )}

      {tab === "Problems" &&
        (summary.enabled ? (
          <CareCases
            key={reportKey}
            customerId={customerId}
            assetId={assetId}
            startReporting={reportKey > 0}
          />
        ) : (
          <p className="text-sm text-(--color-text-muted)">
            Switch KADARI Care on above to report a problem.
          </p>
        ))}

      {tab === "Coverage" && (
        <CoverageTab
          customerId={customerId}
          assetId={assetId}
          enabled={summary.enabled}
        />
      )}

      {tab === "History" && (
        <HistoryTab customerId={customerId} assetId={assetId} />
      )}
    </KadariShell>
  );
}

function CoverageTab({
  customerId,
  assetId,
  enabled,
}: {
  customerId: string;
  assetId: string;
  enabled: boolean;
}) {
  const coverage = useQuery({
    queryKey: ["kadari", "coverage", customerId, assetId],
    queryFn: ({ signal }) => getCoverage(customerId, assetId, signal),
    enabled,
  });

  if (!enabled) {
    return (
      <p className="text-sm text-(--color-text-muted)">
        Switch KADARI Care on above to work out the cover.
      </p>
    );
  }

  if (coverage.isPending) {
    return (
      <Loader2 className="size-4 animate-spin text-(--color-text-muted)" />
    );
  }

  if (coverage.isError || coverage.data.records.length === 0) {
    return (
      <Card>
        <p className="font-semibold">KADARI hasn't confirmed this yet</p>
        <p className="mt-(--space-2) text-sm text-(--color-text-muted)">
          We won't guess. KADARI doesn't have a verified rule for this product
          yet — add the purchase details and a receipt, and KADARI will look
          again as its knowledge grows.
        </p>
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-(--space-5)">
      {coverage.data.records.map((r) => (
        <CoverageRecordCard key={r.coverageType} record={r} />
      ))}
    </div>
  );
}

function CoverageRecordCard({ record }: { record: CoverageRecord }) {
  const how = record.howKadariKnows;
  return (
    <Card>
      <div className="flex flex-wrap items-baseline justify-between gap-(--space-3)">
        <p className="font-semibold">
          {coverageNames[record.coverageType] ?? record.coverageType}
        </p>
        <span className="rounded-(--radius-pill) bg-(--color-surface-sunken) px-(--space-3) py-(--space-1) text-xs font-medium">
          {stateNames[record.state] ?? record.state}
        </span>
      </div>
      <p className="mt-(--space-3) text-sm">{record.explanation}</p>
      {(record.startsOn || record.expiresOn) && (
        <dl className="mt-(--space-4) grid grid-cols-2 gap-(--space-3) text-sm">
          <dt className="text-(--color-text-muted)">Starts</dt>
          <dd>{formatCareDate(record.startsOn) ?? "—"}</dd>
          <dt className="text-(--color-text-muted)">Ends</dt>
          <dd>{formatCareDate(record.expiresOn) ?? "—"}</dd>
        </dl>
      )}
      {record.summary && (
        <p className="mt-(--space-4) text-sm text-(--color-text-muted)">
          {record.summary}
        </p>
      )}
      {how && (
        <details className="mt-(--space-4) text-sm">
          <summary className="kd-focus cursor-pointer font-medium text-(--color-text-link)">
            How KADARI knows this
          </summary>
          <dl className="mt-(--space-3) grid grid-cols-[auto_1fr] gap-x-(--space-4) gap-y-(--space-2)">
            <dt className="text-(--color-text-muted)">Source</dt>
            <dd>
              {how.sourceUrl ? (
                <a
                  href={how.sourceUrl}
                  target="_blank"
                  rel="noreferrer noopener"
                  className="text-(--color-text-link) hover:underline"
                >
                  {how.sourceTitle}
                </a>
              ) : (
                how.sourceTitle
              )}
            </dd>
            <dt className="text-(--color-text-muted)">Last checked</dt>
            <dd>{formatCareDate(how.lastVerifiedAt.slice(0, 10))}</dd>
            <dt className="text-(--color-text-muted)">Worked out</dt>
            <dd>{dateTime.format(new Date(how.decidedAt))}</dd>
          </dl>
        </details>
      )}
    </Card>
  );
}

function HistoryTab({
  customerId,
  assetId,
}: {
  customerId: string;
  assetId: string;
}) {
  const history = useQuery({
    queryKey: ["kadari", "history", customerId, assetId],
    queryFn: ({ signal }) => getAssetHistory(customerId, assetId, signal),
  });

  if (history.isPending) {
    return (
      <Loader2 className="size-4 animate-spin text-(--color-text-muted)" />
    );
  }

  if (history.isError || history.data.entries.length === 0) {
    return (
      <p className="text-sm text-(--color-text-muted)">Nothing recorded yet.</p>
    );
  }

  return (
    <ol className="flex flex-col gap-(--space-4) border-l-2 border-(--color-border) pl-(--space-5)">
      {history.data.entries.map((e) => (
        <li key={e.id} className="flex flex-col gap-(--space-1)">
          <span className="text-xs text-(--color-text-muted)">
            {dateTime.format(new Date(e.occurredAt))}
          </span>
          <span className="text-sm font-medium">{e.summary}</span>
        </li>
      ))}
    </ol>
  );
}

/** A full-width action row with an icon and a chevron (design board 2: "Activate cover", "Something's wrong"). */
function ActionRow({
  icon: Icon,
  label,
  onClick,
  primary = false,
}: {
  icon: typeof Wrench;
  label: string;
  onClick: () => void;
  primary?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "kd-focus flex h-(--tap-target-min) w-full items-center gap-(--space-4) rounded-(--radius-md) px-(--space-5) text-left text-sm font-semibold shadow-(--shadow-1)",
        primary
          ? "bg-(--color-primary) text-(--color-on-primary) hover:bg-(--color-primary-hover)"
          : "border border-(--color-border) bg-(--color-surface) hover:bg-(--color-surface-sunken)",
      )}
    >
      <Icon className="size-5 shrink-0" aria-hidden />
      <span className="flex-1">{label}</span>
      <ChevronRight className="size-4 shrink-0" aria-hidden />
    </button>
  );
}

/** The product's key facts as a list (design board 2's Overview tab). */
function ProductDetails({
  serialNumber,
  purchasedOn,
  retailer,
  coverage,
}: {
  serialNumber?: string | null;
  purchasedOn?: string | null;
  retailer?: string | null;
  coverage: string;
}) {
  const rows: [string, string][] = [
    ["Serial number", serialNumber ?? "Not recorded yet"],
    ["Purchase date", formatCareDate(purchasedOn) ?? "Not recorded yet"],
    ["Purchased from", retailer ?? "Not recorded yet"],
    ["Coverage", coverage],
  ];
  return (
    <dl className="divide-y divide-(--color-border) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) px-(--space-5) shadow-(--shadow-1)">
      {rows.map(([label, value]) => (
        <div
          key={label}
          className="grid grid-cols-[9rem_1fr] gap-(--space-4) py-(--space-4) text-sm"
        >
          <dt className="text-(--color-text-muted)">{label}</dt>
          <dd className="font-medium">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="flex flex-col gap-(--space-4)">
      <h2 className="text-[length:var(--text-h3-size)] leading-(--text-h3-line) font-semibold">
        {title}
      </h2>
      {children}
    </section>
  );
}

function Card({ children }: { children: React.ReactNode }) {
  return (
    <div className="rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-6) shadow-(--shadow-1)">
      {children}
    </div>
  );
}
