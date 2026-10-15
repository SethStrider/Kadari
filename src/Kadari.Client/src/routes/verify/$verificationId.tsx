import { createFileRoute } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { Ban, BadgeCheck, Loader2, SearchX } from "lucide-react";
import type { ReactNode } from "react";

import { ConfidenceBadge } from "@/components/kadari/confidence-badge";
import { categoryLabel, formatMoney } from "@/components/kadari/confidence";
import { KadariShell } from "@/components/kadari/kadari-shell";
import { verifyAssetPassport } from "@/lib/api/kadari";

/** Public, no-login: the page an Asset Passport's QR code / share link opens. */
export const Route = createFileRoute("/verify/$verificationId")({
  component: VerifyAssetPassportPage,
});

const dateFormat = new Intl.DateTimeFormat("en-ZA", { dateStyle: "long" });

function VerifyAssetPassportPage() {
  const { verificationId } = Route.useParams();

  const { data, isPending, isError } = useQuery({
    queryKey: ["kadari", "verify-passport", verificationId],
    queryFn: ({ signal }) => verifyAssetPassport(verificationId, signal),
    retry: false,
  });

  return (
    <KadariShell
      title="Verify an Asset Passport"
      subtitle="Checked live against KADARI’s records."
    >
      <p className="kd-id text-lg font-semibold">{verificationId}</p>

      {isPending && (
        <div className="flex items-center gap-(--space-3) text-(--color-text-muted)">
          <Loader2 className="size-4 animate-spin" /> Checking this passport…
        </div>
      )}

      {isError && (
        <Verdict
          tone="danger"
          icon={<Ban className="size-6" aria-hidden />}
          title="We couldn’t check this passport right now"
          body="Please try again in a moment."
        />
      )}

      {data === null && (
        <Verdict
          tone="neutral"
          icon={<SearchX className="size-6" aria-hidden />}
          title="No passport with this ID"
          body="Check the ID was entered exactly as shown. A passport that doesn’t appear here should not be relied on."
        />
      )}

      {data && (
        <>
          {data.valid ? (
            <Verdict
              tone="success"
              icon={<BadgeCheck className="size-6" aria-hidden />}
              title="Genuine and current"
              body="This Asset Passport was issued by KADARI and is active."
            />
          ) : (
            <Verdict
              tone="danger"
              icon={<Ban className="size-6" aria-hidden />}
              title="This passport is no longer valid"
              body={`Status: ${data.status}. Do not rely on it.`}
            />
          )}

          <dl className="grid grid-cols-1 gap-(--space-5) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-6) text-sm shadow-(--shadow-1) sm:grid-cols-2">
            <Field label="Asset">{data.displayName}</Field>
            <Field label="Category">{categoryLabel(data.category)}</Field>
            <Field label="Confidence">
              <ConfidenceBadge assurance={data.assurance} />
            </Field>
            <Field label="Replacement value">
              <span className="kd-num">
                {formatMoney(data.replacementValueMinor, data.currency)}
              </span>
            </Field>
            <Field label="Evidence items">{String(data.evidenceCount)}</Field>
            <Field label="Last updated">
              {dateFormat.format(new Date(data.lastUpdatedAt))}
            </Field>
          </dl>

          <p className="text-xs text-(--color-text-muted)">
            Confidence reflects the independent evidence behind this asset at
            the last update. The owner’s identity is not disclosed.
          </p>
        </>
      )}
    </KadariShell>
  );
}

const tones = {
  success: "bg-(--color-success-subtle) text-(--color-success)",
  danger: "bg-(--color-danger-subtle) text-(--color-danger)",
  neutral: "bg-(--color-surface-sunken) text-(--color-text-muted)",
} as const;

function Verdict({
  tone,
  icon,
  title,
  body,
}: {
  tone: keyof typeof tones;
  icon: ReactNode;
  title: string;
  body: string;
}) {
  return (
    <div
      role="status"
      className={`flex items-start gap-(--space-4) rounded-(--radius-lg) px-(--space-6) py-(--space-5) ${tones[tone]}`}
    >
      {icon}
      <div>
        <p className="font-semibold">{title}</p>
        <p className="mt-(--space-1) text-sm">{body}</p>
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-(--color-text-muted)">{label}</dt>
      <dd className="mt-(--space-1) font-medium">{children}</dd>
    </div>
  );
}
