import { confidenceLevel } from "./confidence";

/** A confidence level as a pill: icon + label + colour, so confidence never relies on colour alone. */
export function ConfidenceBadge({ assurance }: { assurance: string }) {
  const { token, label, Icon } = confidenceLevel(assurance);

  return (
    <span
      className="inline-flex items-center gap-(--space-2) rounded-(--radius-pill) border px-(--space-3) py-(--space-1) text-xs font-medium"
      style={{
        color: `var(--color-confidence-${token}-text)`,
        background: `var(--color-confidence-${token}-subtle)`,
        borderColor: `var(--color-confidence-${token}-border)`,
      }}
    >
      <Icon className="size-3.5" aria-hidden />
      {label}
    </span>
  );
}
