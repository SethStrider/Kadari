import { useState } from "react";
import { Link } from "@tanstack/react-router";
import {
  CalendarClock,
  ChevronRight,
  Loader2,
  ShieldAlert,
  ShieldCheck,
  ShieldQuestion,
  ShieldX,
} from "lucide-react";
import { toast } from "sonner";

import { formatCareDate } from "@/components/kadari/care-format";
import { KadariButton } from "@/components/kadari/kadari-button";
import {
  careCategories,
  enableCare,
  type CareCategoryName,
  type CareSummary,
} from "@/lib/api/care";
import { cn } from "@/lib/utils";

/** Colour and icon per card state. Status never relies on colour alone — the headline always says it in words. */
const tone: Record<string, { box: string; icon: typeof ShieldCheck }> = {
  Active: {
    box: "border-(--color-success) bg-(--color-success-subtle)",
    icon: ShieldCheck,
  },
  Expiring: {
    box: "border-(--color-warning) bg-(--color-warning-subtle)",
    icon: ShieldAlert,
  },
  Expired: {
    box: "border-(--color-border-strong) bg-(--color-surface-sunken)",
    icon: ShieldX,
  },
  AttentionRequired: {
    box: "border-(--color-info) bg-(--color-info-subtle)",
    icon: ShieldQuestion,
  },
  Unknown: {
    box: "border-(--color-border) bg-(--color-surface)",
    icon: ShieldQuestion,
  },
  NotEnabled: {
    box: "border-(--color-border) bg-(--color-surface)",
    icon: ShieldQuestion,
  },
};

const actionLabels: Record<string, string> = {
  ViewCoverage: "View coverage",
  CheckMyProduct: "Check my product",
  AddPurchaseDetails: "Add purchase details",
  AddProofOfPurchase: "Add proof of purchase",
  WorkOutMyCover: "Work out my cover",
  ViewCase: "View case",
};

/**
 * The Digital Twin Care card (CARE/05 C01): one plain-language headline, the important date, and one next action.
 * Before Care is on, it asks what kind of product this is and switches Care on. Hidden for things Care is not for.
 */
export function CareCard({
  customerId,
  assetId,
  summary,
  onChanged,
  showAction = true,
}: {
  customerId: string;
  assetId: string;
  summary: CareSummary;
  onChanged: () => void;
  /** False on the Care page itself, where the tabs below are the next step. */
  showAction?: boolean;
}) {
  const [category, setCategory] = useState<CareCategoryName>("MajorAppliance");
  const [busy, setBusy] = useState(false);

  if (summary.cardState === "NotCareRelevant") return null;

  const { box, icon: Icon } = tone[summary.cardState] ?? tone.Unknown;
  const nextDate = formatCareDate(summary.nextAttentionOn);

  async function start() {
    setBusy(true);
    try {
      await enableCare(customerId, assetId, category);
      onChanged();
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not start KADARI Care.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div
      className={cn(
        "flex flex-col gap-(--space-5) rounded-(--radius-lg) border p-(--space-6) shadow-(--shadow-1)",
        box,
      )}
    >
      <div className="flex items-start gap-(--space-4)">
        <Icon className="mt-(--space-1) size-6 shrink-0" aria-hidden />
        <div className="flex flex-col gap-(--space-2)">
          <p className="text-[length:var(--text-overline-size)] font-semibold tracking-[0.12em] text-(--color-text-muted) uppercase">
            KADARI Care
          </p>
          <p className="text-[length:var(--text-h3-size)] leading-(--text-h3-line) font-semibold">
            {summary.headline}
          </p>
          <p className="text-sm text-(--color-text-muted)">{summary.detail}</p>
          {nextDate && (
            <p className="flex items-center gap-(--space-2) text-sm">
              <CalendarClock className="size-4" aria-hidden />
              Next important date: {nextDate}
            </p>
          )}
        </div>
      </div>

      {summary.enabled ? (
        showAction && (
          <Link
            to="/$customerId/assets/$assetId/care"
            params={{ customerId, assetId }}
            className="kd-focus inline-flex h-(--tap-target-min) items-center justify-between gap-(--space-3) self-stretch rounded-(--radius-md) bg-(--color-primary) px-(--space-6) text-sm font-semibold text-(--color-on-primary) hover:bg-(--color-primary-hover) sm:self-start"
          >
            {actionLabels[summary.primaryAction] ?? "Open KADARI Care"}
            <ChevronRight className="size-4" aria-hidden />
          </Link>
        )
      ) : (
        <div className="flex flex-col gap-(--space-4) sm:flex-row sm:items-end">
          <label className="flex flex-1 flex-col gap-(--space-2) text-sm font-medium">
            What kind of product is this?
            <select
              className="kd-focus h-(--tap-target-min) rounded-(--radius-md) border border-(--color-border) bg-(--color-surface) px-(--space-4) text-sm"
              value={category}
              onChange={(e) => setCategory(e.target.value as CareCategoryName)}
            >
              {Object.entries(careCategories).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <KadariButton type="button" onClick={start} disabled={busy}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            Work out my cover
          </KadariButton>
        </div>
      )}
    </div>
  );
}
