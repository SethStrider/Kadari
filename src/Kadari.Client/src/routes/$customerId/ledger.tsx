import { createFileRoute, Link } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Camera, Loader2 } from "lucide-react";

import {
  demoToolsEnabled,
  getLedger,
  seedDemoHousehold,
  type Ledger,
} from "@/lib/api/kadari";
import { ConfidenceBadge } from "@/components/kadari/confidence-badge";
import { AssetPosition } from "@/components/kadari/asset-position";
import { CareReminders } from "@/components/kadari/care-reminders";
import { formatMoney as money } from "@/components/kadari/confidence";
import { KadariShell } from "@/components/kadari/kadari-shell";

export const Route = createFileRoute("/$customerId/ledger")({
  component: LedgerPage,
});

function LedgerPage() {
  const { customerId } = Route.useParams();

  const { data, isPending, isError, error } = useQuery<Ledger>({
    queryKey: ["kadari", "ledger", customerId],
    queryFn: ({ signal }) => getLedger(customerId, signal),
  });

  const queryClient = useQueryClient();
  const sample = useMutation({
    mutationFn: () => seedDemoHousehold(customerId),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: ["kadari", "ledger", customerId],
      }),
  });

  return (
    <KadariShell
      customerId={customerId}
      title="Live Asset Ledger™"
      subtitle="Everything you own, room by room."
      action={
        <Link
          to="/$customerId/capture"
          params={{ customerId }}
          className="kd-focus inline-flex h-(--tap-target-min) shrink-0 items-center gap-(--space-2) rounded-(--radius-md) bg-(--color-primary) px-(--space-5) text-sm font-semibold text-(--color-on-primary) hover:bg-(--color-primary-hover)"
        >
          <Camera className="size-4" aria-hidden /> Capture
        </Link>
      }
    >
      {isPending && (
        <div className="flex items-center gap-(--space-3) text-(--color-text-muted)">
          <Loader2 className="size-4 animate-spin" /> Loading ledger…
        </div>
      )}

      {isError && (
        <p className="rounded-(--radius-md) bg-(--color-danger-subtle) px-(--space-5) py-(--space-4) text-sm text-(--color-danger)">
          {error instanceof Error
            ? error.message
            : "Could not load the ledger."}
        </p>
      )}

      {data && (
        <>
          <AssetPosition ledger={data} />

          <CareReminders customerId={customerId} />

          {data.rooms.length > 0 && (
            <h2 className="-mb-(--space-3) text-sm font-semibold tracking-[0.12em] text-(--color-text-muted) uppercase">
              By room
            </h2>
          )}

          {data.rooms.length === 0 && (
            <div className="flex flex-col items-start gap-(--space-3)">
              <p className="text-sm text-(--color-text-muted)">
                No assets yet — capture a room to get started.
              </p>
              {demoToolsEnabled && (
                <>
                  <button
                    type="button"
                    onClick={() => sample.mutate()}
                    disabled={sample.isPending}
                    className="kd-focus inline-flex h-(--tap-target-min) items-center gap-(--space-2) rounded-(--radius-md) border border-(--color-border) px-(--space-5) text-sm font-semibold hover:bg-(--color-surface)"
                  >
                    {sample.isPending && (
                      <Loader2 className="size-4 animate-spin" aria-hidden />
                    )}
                    Load sample household (demo)
                  </button>
                  {sample.isError && (
                    <p className="text-sm text-(--color-danger)">
                      {sample.error.message}
                    </p>
                  )}
                </>
              )}
            </div>
          )}

          {data.rooms.map((room) => (
            <section
              key={room.room}
              className="overflow-hidden rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) shadow-(--shadow-1)"
            >
              <div className="flex items-center justify-between border-b border-(--color-divider) px-(--space-5) py-(--space-4)">
                <span className="font-semibold">{room.room}</span>
                <span className="kd-num text-sm text-(--color-text-muted)">
                  {money(room.roomTotalMinor, data.currency)}
                </span>
              </div>
              <ul className="divide-y divide-(--color-divider)">
                {room.assets.map((asset) => (
                  <li
                    key={asset.assetId}
                    className="flex flex-wrap items-center justify-between gap-(--space-3) px-(--space-5) py-(--space-4) text-sm"
                  >
                    <Link
                      to="/$customerId/assets/$assetId"
                      params={{ customerId, assetId: asset.assetId }}
                      className="kd-focus font-medium hover:text-(--color-text-link) hover:underline"
                    >
                      {asset.displayName}
                    </Link>
                    <span className="flex items-center gap-(--space-5)">
                      <ConfidenceBadge assurance={asset.assurance} />
                      <span className="kd-num min-w-24 text-right font-semibold">
                        {asset.bestValueMinor === null
                          ? "—"
                          : money(asset.bestValueMinor, data.currency)}
                      </span>
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </>
      )}
    </KadariShell>
  );
}
