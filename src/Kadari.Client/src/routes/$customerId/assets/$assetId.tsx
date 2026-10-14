import { createFileRoute, Link } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, ShieldPlus, RefreshCw } from "lucide-react";
import { toast } from "sonner";

import { KadariButton } from "@/components/kadari/kadari-button";
import { AssetPassportCard } from "@/components/kadari/asset-passport-card";
import { ConfidenceBadge } from "@/components/kadari/confidence-badge";
import { ConfidenceLadder } from "@/components/kadari/confidence-ladder";
import { formatMoney } from "@/components/kadari/confidence";
import { KadariShell } from "@/components/kadari/kadari-shell";
import { OwnershipProofForm } from "@/components/kadari/ownership-proof-form";
import {
  getAsset,
  getAssetPassport,
  mintAssetPassport,
} from "@/lib/api/kadari";

export const Route = createFileRoute("/$customerId/assets/$assetId")({
  component: AssetPage,
});

const evidenceLabels: Record<string, string> = {
  Image: "Photo",
  Identification: "Identification",
  Ownership: "Proof of ownership",
  Value: "Value",
};

const dateFormat = new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium" });

function AssetPage() {
  const { customerId, assetId } = Route.useParams();
  const queryClient = useQueryClient();

  const asset = useQuery({
    queryKey: ["kadari", "asset", customerId, assetId],
    queryFn: ({ signal }) => getAsset(customerId, assetId, signal),
  });
  const passport = useQuery({
    queryKey: ["kadari", "passport", customerId, assetId],
    queryFn: ({ signal }) => getAssetPassport(customerId, assetId, signal),
  });

  const mint = useMutation({
    mutationFn: () => mintAssetPassport(customerId, assetId),
    onSuccess: (minted) => {
      queryClient.setQueryData(
        ["kadari", "passport", customerId, assetId],
        minted,
      );
      toast.success(
        passport.data ? "Passport refreshed." : "Asset Passport issued.",
      );
    },
    onError: (cause) =>
      toast.error(
        cause instanceof Error
          ? cause.message
          : "Could not issue the passport.",
      ),
  });

  const refreshAfterProof = () =>
    queryClient.invalidateQueries({ queryKey: ["kadari"] });

  const backLink = (
    <Link
      to="/$customerId/ledger"
      params={{ customerId }}
      className="kd-focus shrink-0 text-sm font-medium text-(--color-text-link) hover:underline"
    >
      ← Ledger
    </Link>
  );

  if (asset.isPending || asset.isError) {
    return (
      <KadariShell customerId={customerId} title="Asset" action={backLink}>
        {asset.isPending ? (
          <div className="flex items-center gap-(--space-3) text-(--color-text-muted)">
            <Loader2 className="size-4 animate-spin" /> Loading asset…
          </div>
        ) : (
          <p className="rounded-(--radius-md) bg-(--color-danger-subtle) px-(--space-5) py-(--space-4) text-sm text-(--color-danger)">
            {asset.error instanceof Error
              ? asset.error.message
              : "Could not load the asset."}
          </p>
        )}
      </KadariShell>
    );
  }

  const a = asset.data;
  const stale =
    !!passport.data &&
    (passport.data.assurance !== a.assurance ||
      passport.data.evidenceCount !== a.evidence.length);

  return (
    <KadariShell
      customerId={customerId}
      title={a.displayName}
      subtitle={[a.category, a.roomLabel].filter(Boolean).join(" · ")}
      action={backLink}
    >
      <Card>
        <div className="flex flex-wrap items-end justify-between gap-(--space-5)">
          <div>
            <p className="text-[length:var(--text-overline-size)] font-semibold tracking-[0.12em] text-(--color-text-muted) uppercase">
              Replacement value
            </p>
            <p className="kd-num mt-(--space-2) text-[length:var(--text-money-xl-size)] leading-(--text-money-xl-line) font-semibold tracking-[-0.02em]">
              {formatMoney(a.bestValueMinor, a.currency)}
            </p>
          </div>
          <ConfidenceBadge assurance={a.assurance} />
        </div>
      </Card>

      <Section title="Confidence">
        <Card>
          <ConfidenceLadder assurance={a.assurance} />
        </Card>
      </Section>

      <Section title="Add proof">
        <Card>
          <OwnershipProofForm
            customerId={customerId}
            assetId={assetId}
            onRecorded={() => void refreshAfterProof()}
          />
        </Card>
      </Section>

      <Section title="Asset Passport">
        {passport.isPending ? (
          <Loader2 className="size-4 animate-spin text-(--color-text-muted)" />
        ) : passport.data ? (
          <div className="flex flex-col gap-(--space-4)">
            <AssetPassportCard passport={passport.data} stale={stale} />
            {stale && (
              <KadariButton
                type="button"
                onClick={() => mint.mutate()}
                disabled={mint.isPending}
                className="self-start"
              >
                {mint.isPending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <RefreshCw className="size-4" />
                )}
                Refresh passport
              </KadariButton>
            )}
          </div>
        ) : (
          <Card>
            <p className="text-sm text-(--color-text-muted)">
              An Asset Passport is a verifiable record of this asset and the
              evidence behind it — useful for your insurer, a claim or a sale.
            </p>
            <KadariButton
              type="button"
              onClick={() => mint.mutate()}
              disabled={mint.isPending}
              className="mt-(--space-5)"
            >
              {mint.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <ShieldPlus className="size-4" />
              )}
              Issue Asset Passport
            </KadariButton>
          </Card>
        )}
      </Section>

      <Section title={`Evidence (${a.evidence.length})`}>
        <ul className="divide-y divide-(--color-divider) overflow-hidden rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface)">
          {a.evidence.map((e, i) => (
            <li
              key={i}
              className="flex flex-wrap items-center justify-between gap-(--space-3) px-(--space-5) py-(--space-4) text-sm"
            >
              <span>
                <span className="font-medium">
                  {evidenceLabels[e.kind] ?? e.kind}
                </span>
                <span className="ml-(--space-3) text-(--color-text-muted)">
                  {dateFormat.format(new Date(e.assertedAt))}
                </span>
              </span>
              <span className="flex items-center gap-(--space-4)">
                {e.amountMinor !== null && (
                  <span className="kd-num">
                    {formatMoney(e.amountMinor, e.currency)}
                  </span>
                )}
                <span
                  className={`rounded-(--radius-pill) px-(--space-3) py-(--space-1) text-xs font-medium ${
                    e.strength === "Independent"
                      ? "bg-(--color-success-subtle) text-(--color-success)"
                      : "bg-(--color-surface-sunken) text-(--color-text-muted)"
                  }`}
                >
                  {e.strength === "Independent"
                    ? "Independent"
                    : "Self-declared"}
                </span>
              </span>
            </li>
          ))}
        </ul>
      </Section>
    </KadariShell>
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
