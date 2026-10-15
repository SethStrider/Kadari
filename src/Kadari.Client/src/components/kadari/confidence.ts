import {
  BadgeCheck,
  CircleDashed,
  Eye,
  KeyRound,
  ScanSearch,
  type LucideIcon,
} from "lucide-react";

export interface ConfidenceLevel {
  /** Server AssetAssurance enum name. */
  key: string;
  /** Suffix of the --color-confidence-* tokens. */
  token: string;
  label: string;
  Icon: LucideIcon;
  /** What lifts an asset from the previous level to this one — shown as the next step. */
  howToReach: string;
}

/** The AssetAssurance ladder, lowest first. Only independent evidence climbs it. */
export const confidenceLevels: ConfidenceLevel[] = [
  {
    key: "None",
    token: "none",
    label: "Unverified",
    Icon: CircleDashed,
    howToReach: "",
  },
  {
    key: "Observed",
    token: "observed",
    label: "Observed",
    Icon: Eye,
    howToReach: "Capture it with the camera.",
  },
  {
    key: "Identified",
    token: "identified",
    label: "Identified",
    Icon: ScanSearch,
    howToReach: "Let KADARI recognise what it is from a clear photo.",
  },
  {
    key: "OwnershipSupported",
    token: "ownership",
    label: "Ownership supported",
    Icon: KeyRound,
    howToReach: "Add a receipt or the serial number.",
  },
  {
    key: "ValueSupported",
    token: "value",
    label: "Value supported",
    Icon: BadgeCheck,
    howToReach:
      "Add the price from your receipt, or get a professional valuation.",
  },
];

export function confidenceLevel(assurance: string): ConfidenceLevel {
  return (
    confidenceLevels.find((l) => l.key === assurance) ?? confidenceLevels[0]
  );
}

export function formatMoney(
  minor: number | string | null | undefined,
  currency: string | null | undefined,
): string {
  if (minor === null || minor === undefined || !currency) return "—";
  return new Intl.NumberFormat("en-ZA", { style: "currency", currency }).format(
    Number(minor) / 100,
  );
}

/** A human label for an API asset category (the API sends the enum name, e.g. `ArtAndCollectibles`). */
const categoryLabels: Record<string, string> = {
  ArtAndCollectibles: "Art & collectibles",
};

export function categoryLabel(category: string): string {
  return categoryLabels[category] ?? category;
}
