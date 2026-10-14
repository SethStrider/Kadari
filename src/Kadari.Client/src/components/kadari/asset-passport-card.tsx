import { ShieldCheck } from "lucide-react";

import type { AssetPassport } from "@/lib/api/kadari";
import { ConfidenceBadge } from "./confidence-badge";
import { formatMoney } from "./confidence";
import { PassportQr } from "./passport-qr";

const dateFormat = new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium" });

/** The Asset Passport credential: its stable AP- id, status and the evidence snapshot it was minted from. */
export function AssetPassportCard({
  passport,
  stale,
}: {
  passport: AssetPassport;
  /** The asset has moved on since the snapshot — the owner should refresh before sharing. */
  stale: boolean;
}) {
  const active = passport.status === "Active";

  return (
    <div className="overflow-hidden rounded-(--radius-xl) border border-(--color-border) bg-(--color-surface) shadow-(--shadow-2)">
      <div className="flex items-center justify-between gap-(--space-4) bg-(--color-surface-inverse) px-(--space-6) py-(--space-5) text-(--color-text-inverse)">
        <span className="flex items-center gap-(--space-3)">
          <ShieldCheck className="size-5 text-(--color-accent)" aria-hidden />
          <span className="text-[length:var(--text-overline-size)] font-semibold tracking-[0.12em] uppercase">
            Asset Passport
          </span>
        </span>
        <span className="text-xs font-light tracking-[0.18em]">Σ(V × C)</span>
      </div>

      <div className="flex flex-col gap-(--space-5) px-(--space-6) py-(--space-6)">
        <div>
          <p className="text-xs text-(--color-text-muted)">Passport ID</p>
          <p className="kd-id mt-(--space-1) text-xl font-semibold">
            {passport.verificationId}
          </p>
        </div>

        <dl className="grid grid-cols-2 gap-(--space-5) text-sm">
          <Field label="Asset" value={passport.displayName} />
          <Field label="Status" value={active ? "Active" : passport.status} />
          <div>
            <dt className="text-xs text-(--color-text-muted)">Confidence</dt>
            <dd className="mt-(--space-2)">
              <ConfidenceBadge assurance={passport.assurance} />
            </dd>
          </div>
          <Field
            label="Replacement value"
            value={formatMoney(
              passport.replacementValueMinor,
              passport.currency,
            )}
            numeric
          />
          <Field
            label="Evidence items"
            value={String(passport.evidenceCount)}
          />
          <Field
            label="Last updated"
            value={dateFormat.format(new Date(passport.lastUpdatedAt))}
          />
        </dl>

        {stale && (
          <p className="rounded-(--radius-md) bg-(--color-warning-subtle) px-(--space-5) py-(--space-4) text-sm text-(--color-warning)">
            This asset has new evidence since the passport was issued. Refresh
            it before you share it.
          </p>
        )}

        {active && (
          <div className="border-t border-(--color-divider) pt-(--space-5)">
            <PassportQr verificationId={passport.verificationId} />
          </div>
        )}
      </div>
    </div>
  );
}

function Field({
  label,
  value,
  numeric,
}: {
  label: string;
  value: string;
  numeric?: boolean;
}) {
  return (
    <div>
      <dt className="text-xs text-(--color-text-muted)">{label}</dt>
      <dd className={`mt-(--space-1) font-medium ${numeric ? "kd-num" : ""}`}>
        {value}
      </dd>
    </div>
  );
}
