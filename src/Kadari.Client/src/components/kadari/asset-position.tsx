import type { Ledger } from "@/lib/api/kadari";
import { formatMoney } from "./confidence";

/** Assurance levels that rest on independent evidence (a receipt, serial, valuation) — not just a photo. */
const evidenced = new Set(["OwnershipSupported", "ValueSupported"]);

const series = [1, 2, 3, 4, 5, 6].map((n) => `var(--kd-series-${n})`);

interface Slice {
  category: string;
  minor: number;
  share: number;
  color: string;
}

/** Value by category, largest first; anything past the fifth folds into "Other" so the legend stays short. */
function slices(ledger: Ledger): Slice[] {
  const totals = new Map<string, number>();
  for (const asset of ledger.rooms.flatMap((r) => r.assets)) {
    if (asset.bestValueMinor === null) continue;
    totals.set(
      asset.category,
      (totals.get(asset.category) ?? 0) + asset.bestValueMinor,
    );
  }
  const sorted = [...totals.entries()].sort((a, b) => b[1] - a[1]);
  const head = sorted.slice(0, 5);
  const tail = sorted.slice(5).reduce((sum, [, v]) => sum + v, 0);
  if (tail > 0) head.push(["Other", tail]);

  const total = head.reduce((sum, [, v]) => sum + v, 0);
  return head.map(([category, minor], i) => ({
    category,
    minor,
    share: total === 0 ? 0 : minor / total,
    color: series[i],
  }));
}

/** The brand sheet's "Your Asset Position" panel, built only from what the ledger actually knows. */
export function AssetPosition({ ledger }: { ledger: Ledger }) {
  const assets = ledger.rooms.flatMap((r) => r.assets);
  const valued = assets.filter((a) => a.bestValueMinor !== null);
  const total = valued.reduce((sum, a) => sum + (a.bestValueMinor ?? 0), 0);
  const backed = valued
    .filter((a) => evidenced.has(a.assurance))
    .reduce((sum, a) => sum + (a.bestValueMinor ?? 0), 0);
  const backedPercent = total === 0 ? 0 : Math.round((100 * backed) / total);
  const withEvidence = assets.filter((a) => evidenced.has(a.assurance)).length;
  const withEvidencePercent =
    assets.length === 0 ? 0 : Math.round((100 * withEvidence) / assets.length);
  const pie = slices(ledger);

  return (
    <section className="flex flex-col gap-(--space-6) rounded-(--radius-xl) border border-(--color-border) bg-(--color-surface) p-(--space-6) shadow-(--shadow-1)">
      <div className="flex flex-col gap-(--space-7) md:flex-row md:items-center md:justify-between">
        <div className="flex-1">
          <p className="flex items-center gap-(--space-2) text-sm font-medium text-(--color-text-muted)">
            <span className="size-2 rounded-full bg-(--color-accent)" />
            Your Asset Position
          </p>
          <p className="kd-num mt-(--space-3) text-[length:var(--text-money-xl-size)] leading-tight font-semibold tracking-[-0.02em]">
            {formatMoney(
              ledger.recordedReplacementExposureMinor,
              ledger.currency,
            )}
          </p>
          <p className="mt-(--space-4) text-sm text-(--color-text-muted)">
            Backed by evidence{" "}
            <span className="kd-num font-semibold text-(--color-text)">
              {backedPercent}%
            </span>
          </p>
          <div
            className="mt-(--space-2) h-2 max-w-xs overflow-hidden rounded-full bg-(--color-surface-sunken)"
            role="meter"
            aria-label="Share of value backed by independent evidence"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={backedPercent}
          >
            <div
              className="h-full rounded-full bg-(--color-primary)"
              style={{ width: `${backedPercent}%` }}
            />
          </div>
          <p className="mt-(--space-2) text-xs text-(--color-text-muted)">
            Value supported by a receipt, serial number or valuation.
          </p>
        </div>

        {pie.length > 0 && (
          <div className="flex items-center gap-(--space-6)">
            <Donut slices={pie} />
            <ul className="flex flex-col gap-(--space-2) text-xs">
              {pie.map((s) => (
                <li
                  key={s.category}
                  className="flex items-center gap-(--space-3)"
                >
                  <span
                    className="size-2.5 rounded-full"
                    style={{ background: s.color }}
                  />
                  <span className="min-w-20">{s.category}</span>
                  <span className="kd-num text-(--color-text-muted)">
                    {Math.round(s.share * 100)}%
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>

      <dl className="grid grid-cols-1 divide-y divide-(--color-divider) border-t border-(--color-divider) sm:grid-cols-3 sm:divide-x sm:divide-y-0">
        <Tile label="Total assets" value={String(ledger.assetCount)} />
        <Tile label="Assets with evidence" value={`${withEvidencePercent}%`} />
        <Tile label="Valued" value={`${ledger.completenessPercent}%`} />
      </dl>
    </section>
  );
}

function Tile({ label, value }: { label: string; value: string }) {
  return (
    <div className="pt-(--space-5) sm:px-(--space-5) sm:first:pl-0">
      <dt className="text-xs text-(--color-text-muted)">{label}</dt>
      <dd className="kd-num mt-(--space-1) text-xl font-semibold">{value}</dd>
    </div>
  );
}

/** A plain SVG donut: each slice is a stroked circle arc, so no chart library is needed. */
function Donut({ slices }: { slices: Slice[] }) {
  const radius = 15.915; // circumference ≈ 100, so dash lengths read as percentages
  let offset = 25; // start at 12 o'clock
  return (
    <svg
      viewBox="0 0 42 42"
      className="size-32 shrink-0 -rotate-0"
      role="img"
      aria-label={`Value by category: ${slices
        .map((s) => `${s.category} ${Math.round(s.share * 100)}%`)
        .join(", ")}`}
    >
      {slices.map((s) => {
        const length = s.share * 100;
        const circle = (
          <circle
            key={s.category}
            cx="21"
            cy="21"
            r={radius}
            fill="none"
            stroke={s.color}
            strokeWidth="7"
            strokeDasharray={`${length} ${100 - length}`}
            strokeDashoffset={offset}
          />
        );
        offset -= length;
        return circle;
      })}
    </svg>
  );
}
