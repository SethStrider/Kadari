import { useMemo, useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { ExternalLink, Loader2, ShieldPlus } from "lucide-react";
import { toast } from "sonner";

import { formatCareDate } from "@/components/kadari/care-format";
import { KadariButton } from "@/components/kadari/kadari-button";
import {
  authoriseRegistration,
  cancelRegistration,
  completeRegistration,
  dismissOpportunity,
  getRegistration,
  reportRegistrationProblem,
  type Opportunity,
  type Registration,
} from "@/lib/api/care";

const inputClass =
  "kd-focus h-(--tap-target-min) w-full rounded-(--radius-md) border border-(--color-border) bg-(--color-surface) px-(--space-5) text-sm text-(--color-text) placeholder:text-(--color-text-muted)";

const statusNames: Record<string, string> = {
  WaitingUserStep: "Waiting for you",
  Completed: "Registered",
  FailedRetryable: "Try again",
  FailedManual: "A person is looking at this",
  Expired: "Deadline passed",
  Cancelled: "Cancelled",
};

/**
 * Extra cover (CARE/05 C04–C06): the offer with its benefit, deadline, who receives the data and exactly what is
 * shared; explicit authority; then the one step on the official site and the reference that proves it worked.
 */
export function ExtraCover({
  customerId,
  assetId,
  onChanged,
}: {
  customerId: string;
  assetId: string;
  onChanged: () => void;
}) {
  const overview = useQuery({
    queryKey: ["kadari", "registration", customerId, assetId],
    queryFn: ({ signal }) => getRegistration(customerId, assetId, signal),
  });

  if (overview.isPending || overview.isError) return null;
  const { opportunities, registrations } = overview.data;
  if (opportunities.length === 0 && registrations.length === 0) return null;

  return (
    <section className="flex flex-col gap-(--space-4)">
      <h2 className="text-[length:var(--text-h3-size)] leading-(--text-h3-line) font-semibold">
        Extra cover
      </h2>
      {registrations
        .filter(
          (r) =>
            r.status === "WaitingUserStep" || r.status === "FailedRetryable",
        )
        .map((r) => (
          <OpenRegistration
            key={r.registrationId}
            customerId={customerId}
            assetId={assetId}
            registration={r}
            onChanged={onChanged}
          />
        ))}
      {opportunities.map((o) => (
        <Offer
          key={o.opportunityId}
          customerId={customerId}
          assetId={assetId}
          offer={o}
          onChanged={onChanged}
        />
      ))}
      {registrations
        .filter(
          (r) =>
            r.status !== "WaitingUserStep" && r.status !== "FailedRetryable",
        )
        .map((r) => (
          <p
            key={r.registrationId}
            className="rounded-(--radius-md) bg-(--color-surface-sunken) px-(--space-5) py-(--space-4) text-sm"
          >
            {statusNames[r.status] ?? r.status}
            {r.externalReference && ` — reference ${r.externalReference}`}
            {r.registeredOn && `, ${formatCareDate(r.registeredOn)}`}
          </p>
        ))}
    </section>
  );
}

function Offer({
  customerId,
  assetId,
  offer,
  onChanged,
}: {
  customerId: string;
  assetId: string;
  offer: Opportunity;
  onChanged: () => void;
}) {
  const [agreed, setAgreed] = useState(false);
  const [busy, setBusy] = useState(false);
  // One key per offer for the life of this screen, so a double tap or a retried request starts one registration.
  const idempotencyKey = useMemo(() => crypto.randomUUID(), []);

  async function activate() {
    setBusy(true);
    try {
      await authoriseRegistration(
        customerId,
        assetId,
        offer.opportunityId,
        offer.termsVersion,
        idempotencyKey,
      );
      onChanged();
    } catch (cause) {
      toast.error(
        cause instanceof Error
          ? cause.message
          : "Could not start the registration.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function notNow() {
    try {
      await dismissOpportunity(customerId, assetId, offer.opportunityId);
      onChanged();
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not dismiss.",
      );
    }
  }

  const missing = offer.shared.filter((f) => !f.value);

  return (
    <div className="flex flex-col gap-(--space-4) rounded-(--radius-lg) border border-(--color-success) bg-(--color-success-subtle) p-(--space-6)">
      <p className="flex items-center gap-(--space-3) font-semibold">
        <ShieldPlus className="size-5" aria-hidden /> {offer.benefit}
      </p>
      {offer.deadlineOn && (
        <p className="text-sm">
          Register by {formatCareDate(offer.deadlineOn)}.
        </p>
      )}
      <div className="text-sm">
        <p className="font-medium">{offer.provider} will receive only:</p>
        <ul className="mt-(--space-2) list-disc pl-(--space-6)">
          {offer.shared.map((f) => (
            <li key={f.field}>
              {f.label}: {f.value ?? <em>not recorded yet</em>}
            </li>
          ))}
        </ul>
        {missing.length > 0 && (
          <p className="mt-(--space-2) text-(--color-text-muted)">
            You can add what's missing on the official site.
          </p>
        )}
      </div>
      <label className="flex items-start gap-(--space-3) text-sm">
        <input
          type="checkbox"
          className="kd-focus mt-1 size-4"
          checked={agreed}
          onChange={(e) => setAgreed(e.target.checked)}
        />
        <span>{offer.terms}</span>
      </label>
      <div className="flex flex-wrap gap-(--space-3)">
        <KadariButton
          type="button"
          onClick={activate}
          disabled={!agreed || busy}
        >
          {busy && <Loader2 className="size-4 animate-spin" />}
          Activate extra cover
        </KadariButton>
        <KadariButton type="button" variant="secondary" onClick={notNow}>
          Not now
        </KadariButton>
      </div>
    </div>
  );
}

function OpenRegistration({
  customerId,
  assetId,
  registration,
  onChanged,
}: {
  customerId: string;
  assetId: string;
  registration: Registration;
  onChanged: () => void;
}) {
  const [reference, setReference] = useState("");
  const [registeredOn, setRegisteredOn] = useState(
    new Date().toISOString().slice(0, 10),
  );
  const [problem, setProblem] = useState(false);
  const [reason, setReason] = useState("");
  const [siteDown, setSiteDown] = useState(false);
  const [busy, setBusy] = useState(false);

  async function run(action: () => Promise<unknown>, done: string) {
    setBusy(true);
    try {
      await action();
      toast.success(done);
      onChanged();
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Something went wrong.",
      );
    } finally {
      setBusy(false);
    }
  }

  function confirm(event: FormEvent) {
    event.preventDefault();
    if (!reference.trim()) return;
    void run(
      () =>
        completeRegistration(
          customerId,
          assetId,
          registration.registrationId,
          reference.trim(),
          registeredOn,
        ),
      "Registration saved to your asset.",
    );
  }

  const link =
    registration.destinationType === "WebPage"
      ? registration.destination
      : registration.destinationType === "Email"
        ? `mailto:${registration.destination}`
        : `tel:${registration.destination}`;

  return (
    <div className="flex flex-col gap-(--space-4) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-6) shadow-(--shadow-1)">
      <p className="font-semibold">
        {registration.status === "FailedRetryable"
          ? "The official site wasn't available — try again"
          : "One step left: register on the official site"}
      </p>
      {registration.instructions && (
        <p className="text-sm text-(--color-text-muted)">
          {registration.instructions}
        </p>
      )}
      <a
        href={link}
        target="_blank"
        rel="noreferrer noopener"
        className="kd-focus inline-flex items-center gap-(--space-2) self-start text-sm font-medium text-(--color-text-link) hover:underline"
      >
        Open the official registration{" "}
        <ExternalLink className="size-4" aria-hidden />
      </a>
      <ul className="list-disc pl-(--space-6) text-sm">
        {registration.shared.map((f) => (
          <li key={f.field}>
            {f.label}: {f.value ?? "—"}
          </li>
        ))}
      </ul>

      <form onSubmit={confirm} className="grid gap-(--space-4) sm:grid-cols-2">
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Confirmation or reference number
          <input
            className={inputClass}
            value={reference}
            onChange={(e) => setReference(e.target.value)}
            maxLength={120}
            placeholder="As the provider showed it"
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Date registered
          <input
            className={inputClass}
            type="date"
            value={registeredOn}
            max={new Date().toISOString().slice(0, 10)}
            onChange={(e) => setRegisteredOn(e.target.value)}
          />
        </label>
        <KadariButton
          type="submit"
          disabled={busy || !reference.trim()}
          className="self-start"
        >
          I've registered
        </KadariButton>
      </form>

      {problem ? (
        <div className="flex flex-col gap-(--space-3) text-sm">
          <label className="flex flex-col gap-(--space-2) font-medium">
            What happened?
            <input
              className={inputClass}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={500}
            />
          </label>
          <label className="flex items-center gap-(--space-3)">
            <input
              type="checkbox"
              className="kd-focus size-4"
              checked={siteDown}
              onChange={(e) => setSiteDown(e.target.checked)}
            />
            The official site or phone line wasn't working
          </label>
          <KadariButton
            type="button"
            variant="secondary"
            disabled={busy || !reason.trim()}
            className="self-start"
            onClick={() =>
              void run(
                () =>
                  reportRegistrationProblem(
                    customerId,
                    assetId,
                    registration.registrationId,
                    reason.trim(),
                    siteDown,
                  ),
                siteDown
                  ? "Saved. Try again when the site is back."
                  : "Saved. A person will look at this.",
              )
            }
          >
            Send
          </KadariButton>
        </div>
      ) : (
        <div className="flex flex-wrap gap-(--space-4) text-sm">
          <button
            type="button"
            className="kd-focus text-(--color-text-link) hover:underline"
            onClick={() => setProblem(true)}
          >
            It didn't work
          </button>
          <button
            type="button"
            className="kd-focus text-(--color-text-muted) hover:underline"
            onClick={() =>
              void run(
                () =>
                  cancelRegistration(
                    customerId,
                    assetId,
                    registration.registrationId,
                  ),
                "Registration cancelled.",
              )
            }
          >
            Cancel
          </button>
        </div>
      )}
    </div>
  );
}
