import { useState, type FormEvent } from "react";
import { Loader2, Save } from "lucide-react";
import { toast } from "sonner";

import { KadariButton } from "@/components/kadari/kadari-button";
import { recordPurchaseDetails, type PurchaseDetails } from "@/lib/api/care";

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
      <div className="grid gap-(--space-5) sm:grid-cols-2">
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Brand
          <input
            className={inputClass}
            value={brand}
            onChange={(e) => setBrand(e.target.value)}
            placeholder="e.g. Bosch"
            maxLength={120}
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Model
          <input
            className={inputClass}
            value={model}
            onChange={(e) => setModel(e.target.value)}
            placeholder="On the label, e.g. WGG244A0ZA"
            maxLength={120}
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Bought from
          <input
            className={inputClass}
            value={retailer}
            onChange={(e) => setRetailer(e.target.value)}
            placeholder="e.g. Makro"
            maxLength={120}
          />
        </label>
        <label className="flex flex-col gap-(--space-2) text-sm font-medium">
          Purchase date
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
