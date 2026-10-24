import { useMemo, useState, type FormEvent, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  Building2,
  CheckCircle2,
  ChevronRight,
  CircleDashed,
  Copy,
  ExternalLink,
  Home,
  Loader2,
  Search,
  ShieldCheck,
  Store,
  Truck,
  Wrench,
  type LucideIcon,
} from "lucide-react";
import { toast } from "sonner";

import { formatCareDate } from "@/components/kadari/care-format";
import { KadariButton } from "@/components/kadari/kadari-button";
import {
  acceptCaseRoute,
  addCaseCommitment,
  addCaseReference,
  bookCaseAppointment,
  getCase,
  getCases,
  issueClasses,
  keepCaseCommitment,
  markCaseSent,
  outcomes,
  providerStatuses,
  recordProviderUpdate,
  reportIssue,
  resolveCase,
  settleCaseAppointment,
  withdrawCase,
  type CareCase,
  type IssueClassName,
} from "@/lib/api/cases";
import { cn } from "@/lib/utils";

const inputClass =
  "kd-focus h-(--tap-target-min) w-full rounded-(--radius-md) border border-(--color-border) bg-(--color-surface) px-(--space-5) text-sm text-(--color-text) placeholder:text-(--color-text-muted)";

const dateTime = new Intl.DateTimeFormat("en-ZA", {
  dateStyle: "medium",
  timeStyle: "short",
});

const confidenceNote: Record<string, string> = {
  Green: "Verified route for this product.",
  Amber: "Likely route — check it suits you before going ahead.",
  Red: "A KADARI Care specialist will find the right place for this.",
};

/** An icon per route, as on the design boards — the label always says it in words too. */
const routeIcons: Record<string, LucideIcon> = {
  RetailerReturn: Store,
  RetailerCredit: Store,
  OemDirect: Building2,
  WarrantyAdmin: ShieldCheck,
  AuthorisedService: Wrench,
  CarryIn: Wrench,
  Onsite: Home,
  Collection: Truck,
  ProtectionProvider: ShieldCheck,
  ResearchRequired: Search,
};

/** Case status as a pill (like the coverage state pill): colour plus words, never colour alone. */
function statusTone(status: string, outcome?: string | null) {
  if (status === "Resolved")
    return "border-(--color-success) bg-(--color-success-subtle)";
  if (
    status === "Rejected" ||
    status === "ClosedWithdrawn" ||
    outcome === "Rejected"
  )
    return "border-(--color-border-strong) bg-(--color-surface-sunken)";
  if (status === "WaitingCustomer")
    return "border-(--color-warning) bg-(--color-warning-subtle)";
  return "border-(--color-info) bg-(--color-info-subtle)";
}

function StatusPill({
  status,
  label,
  outcome,
}: {
  status: string;
  label: string;
  outcome?: string | null;
}) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center rounded-(--radius-pill) border px-(--space-3) py-(--space-1) text-xs font-medium",
        statusTone(status, outcome),
      )}
    >
      {label.charAt(0).toUpperCase() + label.slice(1)}
    </span>
  );
}

function errorText(cause: unknown, fallback: string) {
  return cause instanceof Error ? cause.message : fallback;
}

/**
 * "Something's wrong" and the asset's cases (CARE/05 C10–C27): report a problem, see where it goes and send it through
 * the provider's own channel, then keep the references, replies, promises and appointments until it's sorted.
 */
export function CareCases({
  customerId,
  assetId,
}: {
  customerId: string;
  assetId: string;
}) {
  const queryClient = useQueryClient();
  const [reporting, setReporting] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);

  const cases = useQuery({
    queryKey: ["kadari", "cases", customerId, assetId],
    queryFn: ({ signal }) => getCases(customerId, assetId, signal),
  });

  const refresh = () =>
    void queryClient.invalidateQueries({ queryKey: ["kadari"] });

  if (selected) {
    return (
      <CaseDetail
        customerId={customerId}
        assetId={assetId}
        caseId={selected}
        onBack={() => setSelected(null)}
        onChanged={refresh}
      />
    );
  }

  return (
    <div className="flex flex-col gap-(--space-5)">
      {reporting ? (
        <ReportIssueForm
          customerId={customerId}
          assetId={assetId}
          onCancel={() => setReporting(false)}
          onOpened={(id) => {
            setReporting(false);
            setSelected(id);
            refresh();
          }}
        />
      ) : (
        <KadariButton
          type="button"
          className="self-start"
          onClick={() => setReporting(true)}
        >
          <Wrench className="size-4" aria-hidden />
          Something's wrong
        </KadariButton>
      )}

      {cases.isPending && (
        <Loader2 className="size-4 animate-spin text-(--color-text-muted)" />
      )}
      {cases.data?.length === 0 && !reporting && (
        <p className="text-sm text-(--color-text-muted)">
          No problems reported. If something goes wrong, KADARI will help you
          get it sorted.
        </p>
      )}
      <ul className="flex flex-col gap-(--space-3)">
        {cases.data?.map((c) => (
          <li key={c.caseId}>
            <button
              type="button"
              onClick={() => setSelected(c.caseId)}
              className="kd-focus flex w-full items-center gap-(--space-4) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-5) text-left shadow-(--shadow-1) hover:bg-(--color-surface-sunken)"
            >
              <span className="flex min-w-0 flex-1 flex-col gap-(--space-1)">
                <span className="flex flex-wrap items-center gap-(--space-3)">
                  <span className="font-medium">{c.title}</span>
                  <StatusPill
                    status={c.status}
                    label={c.statusLabel}
                    outcome={c.outcome}
                  />
                </span>
                <span className="text-sm text-(--color-text-muted)">
                  Opened {dateTime.format(new Date(c.openedAt))}
                  {c.nextActionOn &&
                    ` · next: ${formatCareDate(c.nextActionOn)}`}
                </span>
                {c.hasOverduePromise && (
                  <span className="text-sm font-medium text-(--color-danger)">
                    A promised reply is overdue
                  </span>
                )}
                {c.needsRoute && !c.outcome && (
                  <span className="text-sm text-(--color-text-muted)">
                    A KADARI Care specialist is finding the right place.
                  </span>
                )}
              </span>
              <ChevronRight
                className="size-4 shrink-0 text-(--color-text-muted)"
                aria-hidden
              />
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function ReportIssueForm({
  customerId,
  assetId,
  onCancel,
  onOpened,
}: {
  customerId: string;
  assetId: string;
  onCancel: () => void;
  onOpened: (caseId: string) => void;
}) {
  const [issueClass, setIssueClass] = useState<IssueClassName>("Fault");
  const [description, setDescription] = useState("");
  const [unsafe, setUnsafe] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!description.trim()) return;
    setBusy(true);
    try {
      const opened = await reportIssue(
        customerId,
        assetId,
        issueClass,
        description.trim(),
        unsafe,
      );
      onOpened(opened.caseId);
    } catch (cause) {
      toast.error(errorText(cause, "Could not report the problem."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form
      onSubmit={submit}
      className="flex flex-col gap-(--space-5) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-6) shadow-(--shadow-1)"
    >
      <h2 className="text-[length:var(--text-h3-size)] leading-(--text-h3-line) font-semibold">
        What's wrong?
      </h2>
      <fieldset className="flex flex-col gap-(--space-3)">
        <legend className="sr-only">What kind of problem is it?</legend>
        {(Object.keys(issueClasses) as IssueClassName[]).map((k) => (
          <label
            key={k}
            className={cn(
              "flex cursor-pointer items-center gap-(--space-4) rounded-(--radius-md) border p-(--space-4) text-sm has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-(--color-focus-ring)",
              issueClass === k
                ? "border-(--color-primary) bg-(--color-primary-subtle) font-medium"
                : "border-(--color-border) bg-(--color-surface) hover:bg-(--color-surface-sunken)",
            )}
          >
            <input
              type="radio"
              name="issue"
              className="sr-only"
              checked={issueClass === k}
              onChange={() => setIssueClass(k)}
            />
            <span className="flex-1">{issueClasses[k]}</span>
            {issueClass === k ? (
              <CheckCircle2
                className="size-5 text-(--color-primary)"
                aria-hidden
              />
            ) : (
              <ChevronRight
                className="size-4 text-(--color-text-muted)"
                aria-hidden
              />
            )}
          </label>
        ))}
      </fieldset>
      <label className="flex flex-col gap-(--space-2) text-sm font-medium">
        Describe it in your own words
        <textarea
          className={cn(inputClass, "h-28 py-(--space-3)")}
          maxLength={2000}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="e.g. The drum doesn't spin and it shows E18"
          required
        />
      </label>
      <label className="flex items-start gap-(--space-3) text-sm">
        <input
          type="checkbox"
          className="kd-focus mt-1 size-4"
          checked={unsafe}
          onChange={(e) => setUnsafe(e.target.checked)}
        />
        There's smoke, a burning or gas smell, sparks, a shock, overheating or a
        swollen battery
      </label>
      <div className="flex flex-wrap gap-(--space-3)">
        <KadariButton type="submit" disabled={busy || !description.trim()}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          Continue
        </KadariButton>
        <KadariButton type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </KadariButton>
      </div>
    </form>
  );
}

function CaseDetail({
  customerId,
  assetId,
  caseId,
  onBack,
  onChanged,
}: {
  customerId: string;
  assetId: string;
  caseId: string;
  onBack: () => void;
  onChanged: () => void;
}) {
  const detail = useQuery({
    queryKey: ["kadari", "case", customerId, assetId, caseId],
    queryFn: ({ signal }) => getCase(customerId, assetId, caseId, signal),
  });

  const back = (
    <button
      type="button"
      onClick={onBack}
      className="kd-focus self-start text-sm font-medium text-(--color-text-link) hover:underline"
    >
      ← All problems
    </button>
  );

  if (detail.isPending)
    return (
      <Loader2 className="size-4 animate-spin text-(--color-text-muted)" />
    );
  if (detail.isError)
    return (
      <div className="flex flex-col gap-(--space-4)">
        {back}
        <p className="text-sm">Could not load this case.</p>
      </div>
    );

  const c = detail.data;
  const open = !c.closedAt;
  const act = async (run: () => Promise<unknown>, failure: string) => {
    try {
      await run();
      onChanged();
    } catch (cause) {
      toast.error(errorText(cause, failure));
    }
  };

  return (
    <div className="flex flex-col gap-(--space-6)">
      {back}
      <div className="flex flex-wrap items-start justify-between gap-(--space-3)">
        <h2 className="text-[length:var(--text-h3-size)] leading-(--text-h3-line) font-semibold">
          {c.title}
        </h2>
        <StatusPill
          status={c.status}
          label={c.statusLabel}
          outcome={c.outcome}
        />
      </div>

      {c.outcome && (
        <div
          className={cn(
            "flex items-start gap-(--space-4) rounded-(--radius-lg) border p-(--space-5) shadow-(--shadow-1)",
            c.status === "Resolved"
              ? "border-(--color-success) bg-(--color-success-subtle)"
              : "border-(--color-border-strong) bg-(--color-surface-sunken)",
          )}
        >
          <CheckCircle2
            className="mt-(--space-1) size-6 shrink-0"
            aria-hidden
          />
          <div>
            <p className="font-semibold">
              {c.status === "Resolved" ? "Resolved — " : "Closed — "}
              {outcomes[c.outcome as keyof typeof outcomes] ?? c.outcome}
            </p>
            <p className="text-sm">
              {c.outcomeNotes ?? "Saved to this item's history."}
            </p>
          </div>
        </div>
      )}

      {c.safetyGuidance && (
        <div
          role="alert"
          className="flex gap-(--space-3) rounded-(--radius-lg) border border-(--color-danger) bg-(--color-danger-subtle) p-(--space-5) text-sm"
        >
          <AlertTriangle
            className="size-5 shrink-0 text-(--color-danger)"
            aria-hidden
          />
          <p>
            <strong>Safety first.</strong> {c.safetyGuidance}
          </p>
        </div>
      )}

      <Block title="Where this goes">
        <RouteHeading routeType={c.route.routeType}>
          {c.route.needsRoute
            ? "Being researched"
            : c.route.label.charAt(0).toUpperCase() + c.route.label.slice(1)}
          {c.route.targetName && ` — ${c.route.targetName}`}
        </RouteHeading>
        <p className="text-sm text-(--color-text-muted)">
          {confidenceNote[c.route.confidence]}
        </p>
        {c.route.instructions && (
          <p className="text-sm">{c.route.instructions}</p>
        )}
        {open && !c.route.needsRoute && !c.route.accepted && (
          <KadariButton
            type="button"
            className="self-start"
            onClick={() =>
              act(
                () => acceptCaseRoute(customerId, assetId, caseId),
                "Could not confirm.",
              )
            }
          >
            Use this route
          </KadariButton>
        )}
      </Block>

      {open && c.pack && c.route.accepted && c.status === "Preparing" && (
        <SendPack
          customerId={customerId}
          assetId={assetId}
          detail={c}
          onSent={onChanged}
        />
      )}

      {open && c.status !== "Preparing" && (
        <FollowUp
          customerId={customerId}
          assetId={assetId}
          detail={c}
          act={act}
        />
      )}

      {c.references.length > 0 && (
        <Block title="References">
          <ul className="text-sm">
            {c.references.map((r) => (
              <li key={r.referenceId}>
                {r.party}: <strong>{r.reference}</strong>
              </li>
            ))}
          </ul>
        </Block>
      )}

      {c.commitments.length > 0 && (
        <Block title="Promises">
          <ul className="flex flex-col gap-(--space-3) text-sm">
            {c.commitments.map((p) => (
              <li
                key={p.commitmentId}
                className="flex flex-wrap items-center gap-(--space-3)"
              >
                <span
                  className={cn(
                    p.overdue && "font-medium text-(--color-danger)",
                  )}
                >
                  {p.party === "Provider" ? "They" : "You"}: {p.description} —
                  by {formatCareDate(p.dueOn)}
                  {p.overdue && " (overdue)"}
                  {p.status !== "Open" && ` · ${p.status.toLowerCase()}`}
                </span>
                {open && p.status === "Open" && (
                  <KadariButton
                    type="button"
                    variant="secondary"
                    onClick={() =>
                      act(
                        () =>
                          keepCaseCommitment(
                            customerId,
                            assetId,
                            caseId,
                            p.commitmentId,
                          ),
                        "Could not update.",
                      )
                    }
                  >
                    Done
                  </KadariButton>
                )}
              </li>
            ))}
          </ul>
        </Block>
      )}

      {c.appointments.length > 0 && (
        <Block title="Appointments">
          <ul className="flex flex-col gap-(--space-3) text-sm">
            {c.appointments.map((a) => (
              <li
                key={a.appointmentId}
                className="flex flex-wrap items-center gap-(--space-3)"
              >
                <span>
                  {dateTime.format(new Date(a.startsAt))}
                  {a.location && ` · ${a.location}`} · {a.status.toLowerCase()}
                </span>
                {open && a.status === "Booked" && (
                  <>
                    <KadariButton
                      type="button"
                      variant="secondary"
                      onClick={() =>
                        act(
                          () =>
                            settleCaseAppointment(
                              customerId,
                              assetId,
                              caseId,
                              a.appointmentId,
                              true,
                            ),
                          "Could not update.",
                        )
                      }
                    >
                      It happened
                    </KadariButton>
                    <KadariButton
                      type="button"
                      variant="secondary"
                      onClick={() =>
                        act(
                          () =>
                            settleCaseAppointment(
                              customerId,
                              assetId,
                              caseId,
                              a.appointmentId,
                              false,
                            ),
                          "Could not update.",
                        )
                      }
                    >
                      Cancelled
                    </KadariButton>
                  </>
                )}
              </li>
            ))}
          </ul>
        </Block>
      )}

      <Block title="Timeline">
        <ol className="flex flex-col gap-(--space-4)">
          {c.timeline.map((e, i) => (
            <li key={i} className="flex gap-(--space-3)">
              <CheckCircle2
                className="mt-(--space-1) size-4 shrink-0 text-(--color-success)"
                aria-hidden
              />
              <span className="flex flex-col gap-(--space-1)">
                <span className="text-xs text-(--color-text-muted)">
                  {dateTime.format(new Date(e.occurredAt))}
                </span>
                <span className="text-sm font-medium">{e.summary}</span>
              </span>
            </li>
          ))}
        </ol>
      </Block>

      {open && (
        <KadariButton
          type="button"
          variant="secondary"
          className="self-start"
          onClick={() =>
            act(
              () => withdrawCase(customerId, assetId, caseId),
              "Could not close.",
            )
          }
        >
          I don't need this any more
        </KadariButton>
      )}
    </div>
  );
}

function SendPack({
  customerId,
  assetId,
  detail,
  onSent,
}: {
  customerId: string;
  assetId: string;
  detail: CareCase;
  onSent: () => void;
}) {
  const pack = detail.pack!;
  const [agreed, setAgreed] = useState(false);
  const [busy, setBusy] = useState(false);
  // One key per pack for the life of this screen, so a double tap records one handoff.
  const idempotencyKey = useMemo(() => crypto.randomUUID(), []);

  async function sent() {
    setBusy(true);
    try {
      await markCaseSent(
        customerId,
        assetId,
        detail.caseId,
        pack.termsVersion,
        idempotencyKey,
      );
      onSent();
    } catch (cause) {
      toast.error(errorText(cause, "Could not record that it was sent."));
    } finally {
      setBusy(false);
    }
  }

  async function copy() {
    try {
      await navigator.clipboard.writeText(pack.message);
      toast.success("Copied");
    } catch {
      toast.error("Could not copy — select the text instead.");
    }
  }

  const handoff = pack.handoff;
  return (
    <Block title="Send it">
      <p className="text-sm font-medium">
        {detail.route.targetName ?? "The provider"} will receive only:
      </p>
      <ul className="flex flex-col gap-(--space-2) text-sm">
        {pack.shared.map((f, i) => (
          <li
            key={`${f.field}-${i}`}
            className="flex items-start gap-(--space-3)"
          >
            {f.value ? (
              <CheckCircle2
                className="mt-0.5 size-4 shrink-0 text-(--color-success)"
                aria-hidden
              />
            ) : (
              <CircleDashed
                className="mt-0.5 size-4 shrink-0 text-(--color-text-muted)"
                aria-hidden
              />
            )}
            <span>
              {f.label}:{" "}
              {f.field === "Issue"
                ? "your description"
                : (f.value ?? <em>not recorded yet</em>)}
            </span>
          </li>
        ))}
      </ul>
      <pre className="max-h-48 overflow-auto rounded-(--radius-md) bg-(--color-surface-sunken) p-(--space-4) text-xs whitespace-pre-wrap">
        {pack.message}
      </pre>
      <div className="flex flex-wrap gap-(--space-3)">
        <KadariButton type="button" variant="secondary" onClick={copy}>
          <Copy className="size-4" aria-hidden /> Copy message
        </KadariButton>
        {handoff?.link && (
          <a
            href={handoff.link}
            target="_blank"
            rel="noreferrer"
            className="kd-focus inline-flex h-(--tap-target-min) items-center gap-(--space-2) rounded-(--radius-md) border border-(--color-border) px-(--space-5) text-sm font-medium"
          >
            Open{" "}
            {handoff.channelType === "WebPage"
              ? "their support page"
              : handoff.channelType === "WhatsApp"
                ? "WhatsApp"
                : handoff.channelType === "Email"
                  ? "email"
                  : "phone"}
            <ExternalLink className="size-4" aria-hidden />
          </a>
        )}
      </div>
      <label className="flex items-start gap-(--space-3) text-sm">
        <input
          type="checkbox"
          className="kd-focus mt-1 size-4"
          checked={agreed}
          onChange={(e) => setAgreed(e.target.checked)}
        />
        <span>{pack.terms}</span>
      </label>
      <KadariButton
        type="button"
        className="self-start"
        disabled={!agreed || busy}
        onClick={sent}
      >
        {busy && <Loader2 className="size-4 animate-spin" />}
        I've sent it
      </KadariButton>
    </Block>
  );
}

function FollowUp({
  customerId,
  assetId,
  detail,
  act,
}: {
  customerId: string;
  assetId: string;
  detail: CareCase;
  act: (run: () => Promise<unknown>, failure: string) => Promise<void>;
}) {
  const id = detail.caseId;
  const [party, setParty] = useState("");
  const [reference, setReference] = useState("");
  const [status, setStatus] = useState("");
  const [note, setNote] = useState("");
  const [promise, setPromise] = useState("");
  const [due, setDue] = useState("");
  const [when, setWhen] = useState("");
  const [where, setWhere] = useState("");
  const [outcome, setOutcome] = useState("");
  const [notes, setNotes] = useState("");

  return (
    <Block title="Keep track">
      <MiniForm
        label="Add a reference number"
        disabled={!party.trim() || !reference.trim()}
        onSubmit={() =>
          act(
            () =>
              addCaseReference(
                customerId,
                assetId,
                id,
                party.trim(),
                reference.trim(),
              ),
            "Could not add the reference.",
          ).then(() => setReference(""))
        }
      >
        <Field label="Who gave it">
          <input
            className={inputClass}
            placeholder="From (e.g. Makro)"
            value={party}
            onChange={(e) => setParty(e.target.value)}
            maxLength={120}
          />
        </Field>
        <Field label="Reference number">
          <input
            className={inputClass}
            placeholder="e.g. RMA-55120"
            value={reference}
            onChange={(e) => setReference(e.target.value)}
            maxLength={120}
          />
        </Field>
      </MiniForm>

      <MiniForm
        label="They replied"
        disabled={!status}
        onSubmit={() =>
          act(
            () =>
              recordProviderUpdate(
                customerId,
                assetId,
                id,
                status,
                note.trim(),
              ),
            "Could not record the reply.",
          ).then(() => setNote(""))
        }
      >
        <Field label="Where is it now?">
          <select
            className={inputClass}
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">Choose…</option>
            {Object.entries(providerStatuses).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </Field>
        <Field label="What they said (optional)">
          <input
            className={inputClass}

            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={2000}
          />
        </Field>
      </MiniForm>

      <MiniForm
        label="They promised something"
        disabled={!promise.trim() || !due}
        onSubmit={() =>
          act(
            () =>
              addCaseCommitment(
                customerId,
                assetId,
                id,
                "Provider",
                promise.trim(),
                due,
              ),
            "Could not save the promise.",
          ).then(() => setPromise(""))
        }
      >
        <Field label="What they promised">
          <input
            className={inputClass}
            placeholder="e.g. Call back with a decision"
            value={promise}
            onChange={(e) => setPromise(e.target.value)}
            maxLength={280}
          />
        </Field>
        <Field label="By when">
          <input
            className={inputClass}
            type="date"
            value={due}
            onChange={(e) => setDue(e.target.value)}
          />
        </Field>
      </MiniForm>

      <MiniForm
        label="An appointment was booked"
        disabled={!when}
        onSubmit={() =>
          act(
            () =>
              bookCaseAppointment(
                customerId,
                assetId,
                id,
                new Date(when).toISOString(),
                where.trim(),
              ),
            "Could not save the appointment.",
          )
        }
      >
        <Field label="When">
          <input
            className={inputClass}
            type="datetime-local"
            value={when}
            onChange={(e) => setWhen(e.target.value)}
          />
        </Field>
        <Field label="Where (optional)">
          <input
            className={inputClass}

            value={where}
            onChange={(e) => setWhere(e.target.value)}
            maxLength={500}
          />
        </Field>
      </MiniForm>

      <MiniForm
        label="It's sorted"
        disabled={!outcome}
        onSubmit={() =>
          act(
            () => resolveCase(customerId, assetId, id, outcome, notes.trim()),
            "Could not close the case.",
          )
        }
      >
        <Field label="How did it end?">
          <select
            className={inputClass}
            value={outcome}
            onChange={(e) => setOutcome(e.target.value)}
          >
            <option value="">Choose…</option>
            {Object.entries(outcomes).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Notes (optional)">
          <input
            className={inputClass}

            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            maxLength={1000}
          />
        </Field>
      </MiniForm>
    </Block>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-(--space-2) text-sm font-medium">
      {label}
      {children}
    </label>
  );
}

function MiniForm({
  label,
  disabled,
  onSubmit,
  children,
}: {
  label: string;
  disabled: boolean;
  onSubmit: () => void;
  children: ReactNode;
}) {
  return (
    <form
      className="flex flex-col gap-(--space-3)"
      onSubmit={(e) => {
        e.preventDefault();
        if (!disabled) onSubmit();
      }}
    >
      <p className="text-sm font-medium">{label}</p>
      <div className="grid gap-(--space-3) sm:grid-cols-2">{children}</div>
      <KadariButton
        type="submit"
        variant="secondary"
        className="self-start"
        disabled={disabled}
      >
        Save
      </KadariButton>
    </form>
  );
}

function RouteHeading({
  routeType,
  children,
}: {
  routeType: string;
  children: ReactNode;
}) {
  const Icon = routeIcons[routeType] ?? Search;
  return (
    <p className="flex items-center gap-(--space-3) font-medium">
      <span className="flex size-9 shrink-0 items-center justify-center rounded-(--radius-md) bg-(--color-primary-subtle) text-(--color-on-primary-subtle)">
        <Icon className="size-5" aria-hidden />
      </span>
      <span>{children}</span>
    </p>
  );
}

function Block({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-(--space-3) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-6) shadow-(--shadow-1)">
      <h3 className="font-semibold">{title}</h3>
      {children}
    </section>
  );
}
