import { useState, type FormEvent, type ReactNode } from "react";
import { Loader2, ShieldCheck } from "lucide-react";
import { toast } from "sonner";

import { KadariButton } from "@/components/kadari/kadari-button";
import {
  grantConsent,
  withdrawConsent,
  type ConsentScopeName,
  type KadariConsent,
} from "@/lib/api/kadari";

/** What each consent covers, in the customer's words. Kept in one place so the ask and the settings match. */
const disclosures: Record<
  ConsentScopeName,
  { title: string; body: string; required: boolean }
> = {
  AssetImageProcessing: {
    title: "Process photos of my home and belongings",
    body: "KADARI stores the photos you capture and uses them to identify, value and keep a record of what you own. They are kept as evidence for your insurance and only shared when you choose to.",
    required: true,
  },
  CrossBorderAiEnrichment: {
    title: "Use an AI service outside South Africa for better identification",
    body: "A captured photo may be sent to an AI provider outside South Africa to name the item more precisely. Without this, identification happens on your device and the photo stays in South Africa.",
    required: false,
  },
  CareReminders: {
    title: "Send me KADARI Care reminders",
    body: "KADARI may email or text you when a warranty registration deadline is near or a warranty is about to end. Without this, reminders only appear in the app.",
    required: false,
  },
};

const scopeNames = Object.keys(disclosures) as ConsentScopeName[];

/** Shown before the first capture: the image-processing consent is required, the cross-border one optional. */
export function CaptureConsent({
  customerId,
  onGranted,
}: {
  customerId: string;
  onGranted: () => void;
}) {
  const [imageProcessing, setImageProcessing] = useState(false);
  const [crossBorder, setCrossBorder] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!imageProcessing) return;

    setBusy(true);
    try {
      await grantConsent(customerId, "AssetImageProcessing");
      if (crossBorder) {
        await grantConsent(customerId, "CrossBorderAiEnrichment");
      }
      onGranted();
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not save your choice.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <form
      onSubmit={submit}
      className="flex flex-col gap-(--space-6) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-6) shadow-(--shadow-1)"
    >
      <div className="flex items-start gap-(--space-4)">
        <ShieldCheck
          className="mt-(--space-1) size-5 shrink-0 text-(--color-primary)"
          aria-hidden
        />
        <div>
          <h2 className="font-semibold">Before you capture</h2>
          <p className="mt-(--space-1) text-sm text-(--color-text-muted)">
            You can change these at any time from this screen.
          </p>
        </div>
      </div>

      <ConsentOption
        checked={imageProcessing}
        onChange={setImageProcessing}
        scope="AssetImageProcessing"
      />
      <ConsentOption
        checked={crossBorder}
        onChange={setCrossBorder}
        scope="CrossBorderAiEnrichment"
      />

      <KadariButton
        type="submit"
        disabled={!imageProcessing || busy}
        className="self-start"
      >
        {busy && <Loader2 className="animate-spin" aria-hidden />}
        Agree and start capturing
      </KadariButton>
    </form>
  );
}

function ConsentOption({
  scope,
  checked,
  onChange,
}: {
  scope: ConsentScopeName;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  const { title, body, required } = disclosures[scope];
  return (
    <label className="flex cursor-pointer items-start gap-(--space-4)">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="kd-focus mt-(--space-1) size-4 shrink-0 accent-(--color-primary)"
      />
      <span>
        <span className="text-sm font-medium">
          {title}
          <span className="ml-(--space-2) text-xs font-normal text-(--color-text-muted)">
            {required ? "Required" : "Optional"}
          </span>
        </span>
        <span className="mt-(--space-1) block text-sm text-(--color-text-muted)">
          {body}
        </span>
      </span>
    </label>
  );
}

/** Each consent with its current state and a way to change it — withdrawal is as easy as granting (POPIA). */
export function ConsentSettings({
  customerId,
  consents,
  onChanged,
}: {
  customerId: string;
  consents: KadariConsent[];
  onChanged: () => void;
}) {
  const [busy, setBusy] = useState<ConsentScopeName | null>(null);

  async function toggle(scope: ConsentScopeName, granted: boolean) {
    setBusy(scope);
    try {
      await (granted
        ? withdrawConsent(customerId, scope)
        : grantConsent(customerId, scope));
      onChanged();
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not save your choice.",
      );
    } finally {
      setBusy(null);
    }
  }

  return (
    <details className="rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) px-(--space-6) py-(--space-5) text-sm">
      <summary className="kd-focus cursor-pointer font-medium">
        Privacy choices
      </summary>
      <ul className="mt-(--space-5) flex flex-col gap-(--space-5)">
        {scopeNames.map((scope) => {
          const granted =
            consents.find((c) => c.scope === scope)?.granted ?? false;
          return (
            <li
              key={scope}
              className="flex flex-col gap-(--space-3) sm:flex-row sm:items-start sm:justify-between"
            >
              <Choice title={disclosures[scope].title} granted={granted}>
                {scope === "AssetImageProcessing" && granted
                  ? "Withdrawing stops new captures. To remove what is already recorded, request erasure."
                  : null}
              </Choice>
              <KadariButton
                variant="secondary"
                className="shrink-0 self-start"
                disabled={busy !== null}
                onClick={() => void toggle(scope, granted)}
              >
                {busy === scope && (
                  <Loader2 className="animate-spin" aria-hidden />
                )}
                {granted ? "Withdraw" : "Allow"}
              </KadariButton>
            </li>
          );
        })}
      </ul>
    </details>
  );
}

function Choice({
  title,
  granted,
  children,
}: {
  title: string;
  granted: boolean;
  children: ReactNode;
}) {
  return (
    <div>
      <p className="font-medium">{title}</p>
      <p
        className={`mt-(--space-1) text-xs ${granted ? "text-(--color-success)" : "text-(--color-text-muted)"}`}
      >
        {granted ? "Allowed" : "Not allowed"}
      </p>
      {children && (
        <p className="mt-(--space-1) text-xs text-(--color-text-muted)">
          {children}
        </p>
      )}
    </div>
  );
}
