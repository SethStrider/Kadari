import { useState, type FormEvent } from "react";
import { useRef } from "react";
import { Camera, Loader2, ReceiptText, Save } from "lucide-react";
import { toast } from "sonner";

import { KadariButton } from "@/components/kadari/kadari-button";
import {
  readProductLabel,
  readReceipt,
  recordPurchaseDetails,
  unavailableReasons,
  type PurchaseDetails,
  type PurchaseProposal,
} from "@/lib/api/care";

/** Below this, a read value is shown as "check this". */
const sureEnough = 0.75;

const inputClass =
  "kd-focus h-(--tap-target-min) w-full rounded-(--radius-md) border border-(--color-border) bg-(--color-surface) px-(--space-5) text-sm text-(--color-text) placeholder:text-(--color-text-muted)";

/**
 * What was bought, where and when — the facts KADARI Care reads. Every field is optional; saving adds a new record
 * (a correction never overwrites the old one) and Care re-checks the cover straight away.
 */
export function PurchaseDetailsForm({
  customerId,
  assetId,
  initial,
  onRecorded,
}: {
  customerId: string;
  assetId: string;
  initial?: { [K in keyof PurchaseDetails]?: string | null } | null;
  onRecorded: () => void;
}) {
  const [brand, setBrand] = useState(initial?.brand ?? "");
  const [model, setModel] = useState(initial?.model ?? "");
  const [retailer, setRetailer] = useState(initial?.retailer ?? "");
  const [purchasedOn, setPurchasedOn] = useState(initial?.purchasedOn ?? "");
  const [busy, setBusy] = useState(false);
  const [reading, setReading] = useState<"receipt" | "label" | null>(null);
  const [unsure, setUnsure] = useState<Set<string>>(new Set());
  const [readSerial, setReadSerial] = useState<string | null>(null);
  const labelInput = useRef<HTMLInputElement>(null);

  /** Put what KADARI read into the form for the owner to check — nothing is saved until they press Save. */
  function apply(proposal: PurchaseProposal) {
    if (!proposal.read) {
      toast.info(
        unavailableReasons[proposal.unavailable ?? ""] ??
          "KADARI couldn't read this.",
      );
      return;
    }

    const doubtful = new Set<string>();
    const take = (
      field: string,
      value: { value: string; confidence: number | string } | null,
      set: (v: string) => void,
    ) => {
      if (!value) return;
      set(value.value);
      if (Number(value.confidence) < sureEnough) doubtful.add(field);
    };
    take("brand", proposal.brand, setBrand);
    take("model", proposal.model, setModel);
    take("retailer", proposal.retailer, setRetailer);
    take("purchasedOn", proposal.purchasedOn, setPurchasedOn);
    setReadSerial(proposal.serialNumber?.value ?? null);
    setUnsure(doubtful);
    toast.success("Check what KADARI read, then save.");
  }

  async function read(kind: "receipt" | "label", photo?: File) {
    setReading(kind);
    try {
      apply(
        kind === "receipt"
          ? await readReceipt(customerId, assetId)
          : await readProductLabel(customerId, assetId, photo!),
      );
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not read the document.",
      );
    } finally {
      setReading(null);
    }
  }

  const hint = (field: string) =>
    unsure.has(field) ? (
      <span className="text-xs font-normal text-(--color-warning)">
        Check this
      </span>
    ) : null;

  const today = new Date().toISOString().slice(0, 10);
  const empty = [brand, model, retailer, purchasedOn].every(
    (v) => v.trim() === "",
  );

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (empty) return;

    setBusy(true);
    try {
      await recordPurchaseDetails(customerId, assetId, {
        brand: brand.trim() || undefined,
        model: model.trim() || undefined,
        retailer: retailer.trim() || undefined,
        purchasedOn: purchasedOn || undefined,
      });
      toast.success("Purchase details saved.");
      onRecorded();
    } catch (cause) {
      toast.error(
        cause instanceof Error
          ? cause.message
          : "Could not save the purchase details.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-(--space-5)">
      <div className="flex flex-wrap gap-(--space-3)">
        <KadariButton
          type="button"
          variant="secondary"
          onClick={() => void read("receipt")}
          disabled={reading !== null}
        >
          {reading === "receipt" ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <ReceiptText className="size-4" />
          )}
          Read my receipt
        </KadariButton>
        <KadariButton
          type="button"
          variant="secondary"
          onClick={() => labelInput.current?.click()}
          disabled={reading !== null}
        >
          {reading === "label" ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <Camera className="size-4" />
          )}
          Scan the label
        </KadariButton>
        <input
          ref={labelInput}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          capture="environment"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            e.target.value = "";
            if (file) void read("label", file);
          }}
        />
      </div>
      {readSerial && (
        <p className="rounded-(--radius-md) bg-(--color-info-subtle) px-(--space-5) py-(--space-4) text-sm">
          Serial number on the label:{" "}
          <span className="kd-num">{readSerial}</span>. Add it under proof of
          purchase to keep it with the asset.
        </p>
      )}
      <div className="grid gap-(--space-5) sm:grid-cols-2">
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          <span className="flex items-baseline justify-between gap-(--space-3)">
            Brand {hint("brand")}
          </span>
          <input
            className={inputClass}
            value={brand}
            onChange={(e) => setBrand(e.target.value)}
            placeholder="e.g. Bosch"
            maxLength={120}
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          <span className="flex items-baseline justify-between gap-(--space-3)">
            Model {hint("model")}
          </span>
          <input
            className={inputClass}
            value={model}
            onChange={(e) => setModel(e.target.value)}
            placeholder="On the label, e.g. WGG244A0ZA"
            maxLength={120}
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          <span className="flex items-baseline justify-between gap-(--space-3)">
            Bought from {hint("retailer")}
          </span>
          <input
            className={inputClass}
            value={retailer}
            onChange={(e) => setRetailer(e.target.value)}
            placeholder="e.g. Makro"
            maxLength={120}
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          <span className="flex items-baseline justify-between gap-(--space-3)">
            Purchase date {hint("purchasedOn")}
          </span>
          <input
            className={inputClass}
            type="date"
            value={purchasedOn}
            max={today}
            onChange={(e) => setPurchasedOn(e.target.value)}
          />
        </label>
      </div>
      <KadariButton
        type="submit"
        disabled={busy || empty}
        className="self-start"
      >
        {busy ? (
          <Loader2 className="size-4 animate-spin" />
        ) : (
          <Save className="size-4" />
        )}
        Save purchase details
      </KadariButton>
    </form>
  );
}
