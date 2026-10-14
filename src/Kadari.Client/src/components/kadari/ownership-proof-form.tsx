import { useState, type FormEvent } from "react";
import { Loader2, Upload } from "lucide-react";
import { toast } from "sonner";

import { KadariButton } from "@/components/kadari/kadari-button";
import { recordOwnershipProof } from "@/lib/api/kadari";

const inputClass =
  "kd-focus h-(--tap-target-min) w-full rounded-(--radius-md) border border-(--color-border) bg-(--color-surface) px-(--space-5) text-sm text-(--color-text) placeholder:text-(--color-text-muted)";

/** Attach independent proof — a receipt and/or serial number, optionally the receipt price — to lift confidence. */
export function OwnershipProofForm({
  customerId,
  assetId,
  onRecorded,
}: {
  customerId: string;
  assetId: string;
  onRecorded: () => void;
}) {
  const [receipt, setReceipt] = useState<File | undefined>();
  const [serialNumber, setSerialNumber] = useState("");
  const [price, setPrice] = useState("");
  const [busy, setBusy] = useState(false);

  const amount = price.trim() === "" ? undefined : Number(price);
  const amountInvalid =
    amount !== undefined && (!Number.isFinite(amount) || amount <= 0);
  const empty = !receipt && serialNumber.trim() === "";
  // A price is only independent evidence when the receipt that shows it comes with it.
  const priceNeedsReceipt = amount !== undefined && !receipt;

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (empty || amountInvalid || priceNeedsReceipt) return;

    setBusy(true);
    try {
      await recordOwnershipProof(customerId, assetId, {
        receipt,
        serialNumber: serialNumber.trim() || undefined,
        amountMinor:
          amount === undefined ? undefined : Math.round(amount * 100),
      });
      toast.success("Proof added.");
      setReceipt(undefined);
      setSerialNumber("");
      setPrice("");
      onRecorded();
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not add the proof.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-(--space-5)">
      <label className="flex flex-col gap-(--space-2) text-sm font-medium">
        Receipt or invoice
        <input
          type="file"
          accept="image/*,application/pdf"
          onChange={(e) => setReceipt(e.target.files?.[0])}
          className="kd-focus text-sm text-(--color-text-muted) file:mr-(--space-4) file:rounded-(--radius-sm) file:border-0 file:bg-(--color-primary-subtle) file:px-(--space-4) file:py-(--space-3) file:font-medium file:text-(--color-on-primary-subtle)"
        />
      </label>

      <div className="grid grid-cols-1 gap-(--space-5) sm:grid-cols-2">
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Serial number
          <input
            value={serialNumber}
            onChange={(e) => setSerialNumber(e.target.value)}
            placeholder="e.g. C02XK1Y2JGH5"
            className={inputClass}
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Price on the receipt (R)
          <input
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            inputMode="decimal"
            placeholder="Optional"
            aria-invalid={amountInvalid}
            className={inputClass}
          />
        </label>
      </div>

      {amountInvalid && (
        <p className="text-sm text-(--color-danger)">
          Enter the price as a number, e.g. 12450.00
        </p>
      )}

      {priceNeedsReceipt && !amountInvalid && (
        <p className="text-sm text-(--color-text-muted)">
          Attach the receipt that shows this price.
        </p>
      )}

      <KadariButton
        type="submit"
        disabled={busy || empty || amountInvalid || priceNeedsReceipt}
        className="self-start"
      >
        {busy ? (
          <Loader2 className="size-4 animate-spin" />
        ) : (
          <Upload className="size-4" />
        )}
        Add proof
      </KadariButton>
    </form>
  );
}
