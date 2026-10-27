import { Link } from "@tanstack/react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { BellRing } from "lucide-react";
import { toast } from "sonner";

import {
  dismissCareReminder,
  getCareReminders,
  snoozeCareReminder,
} from "@/lib/api/care";
import { getConsents, grantConsent, withdrawConsent } from "@/lib/api/kadari";

/**
 * Care reminders that need attention now (CARE/05 C07): why it matters, what to do, and "remind me later". On the
 * ledger it lists every asset's; on an asset's Care page only that asset's, with the opt-in for messages.
 */
export function CareReminders({
  customerId,
  assetId,
  showOptIn = false,
}: {
  customerId: string;
  assetId?: string;
  showOptIn?: boolean;
}) {
  const queryClient = useQueryClient();
  const reminders = useQuery({
    queryKey: ["kadari", "care-reminders", customerId, assetId ?? "all"],
    queryFn: ({ signal }) => getCareReminders(customerId, assetId, signal),
  });
  const consents = useQuery({
    queryKey: ["kadari", "consents", customerId],
    queryFn: ({ signal }) => getConsents(customerId, signal),
    enabled: showOptIn,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["kadari"] });

  async function act(action: () => Promise<unknown>) {
    try {
      await action();
      void refresh();
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Something went wrong.",
      );
    }
  }

  const optedIn =
    consents.data?.find((c) => c.scope === "CareReminders")?.granted ?? false;
  const items = reminders.data ?? [];

  if (!showOptIn && items.length === 0) return null;
  if (reminders.data === null) return null;

  return (
    <section className="flex flex-col gap-(--space-4)">
      <h2 className="text-[length:var(--text-h3-size)] leading-(--text-h3-line) font-semibold">
        {assetId ? "Reminders" : "Needs attention"}
      </h2>
      {items.length === 0 && (
        <p className="text-sm text-(--color-text-muted)">
          Nothing needs your attention right now.
        </p>
      )}
      {items.map((r) => (
        <div
          key={r.reminderId}
          className="flex flex-col gap-(--space-3) rounded-(--radius-lg) border border-(--color-warning) bg-(--color-warning-subtle) p-(--space-5)"
        >
          <p className="flex items-center gap-(--space-3) font-semibold">
            <BellRing className="size-4" aria-hidden /> {r.headline}
          </p>
          <p className="text-sm">{r.message}</p>
          <div className="flex flex-wrap gap-(--space-4) text-sm">
            {!assetId && (
              <Link
                to="/$customerId/assets/$assetId/care"
                params={{ customerId, assetId: r.assetId }}
                className="kd-focus font-medium text-(--color-text-link) hover:underline"
              >
                Open {r.assetName}
              </Link>
            )}
            <button
              type="button"
              className="kd-focus text-(--color-text-link) hover:underline"
              onClick={() =>
                void act(() => snoozeCareReminder(customerId, r.reminderId, 7))
              }
            >
              Remind me in a week
            </button>
            <button
              type="button"
              className="kd-focus text-(--color-text-muted) hover:underline"
              onClick={() =>
                void act(() => dismissCareReminder(customerId, r.reminderId))
              }
            >
              Dismiss
            </button>
          </div>
        </div>
      ))}
      {showOptIn && consents.data && (
        <label className="flex items-start gap-(--space-3) text-sm">
          <input
            type="checkbox"
            className="kd-focus mt-1 size-4"
            checked={optedIn}
            onChange={(e) =>
              void act(() =>
                e.target.checked
                  ? grantConsent(customerId, "CareReminders")
                  : withdrawConsent(customerId, "CareReminders"),
              )
            }
          />
          <span>
            Send me Care reminders by email or SMS — registration deadlines and
            warranty expiry only.
          </span>
        </label>
      )}
    </section>
  );
}
