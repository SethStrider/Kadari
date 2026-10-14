import { Check } from "lucide-react";

import { confidenceLevel, confidenceLevels } from "./confidence";

/** The four climbable rungs (Observed → Value supported) with the asset's position and its next step. */
export function ConfidenceLadder({ assurance }: { assurance: string }) {
  const rungs = confidenceLevels.slice(1);
  const current = confidenceLevels.indexOf(confidenceLevel(assurance));
  const next = confidenceLevels[current + 1];

  return (
    <div className="flex flex-col gap-(--space-5)">
      <ol className="grid grid-cols-4 gap-(--space-2)" aria-label="Confidence">
        {rungs.map((rung, i) => {
          const reached = i + 1 <= current;
          return (
            <li key={rung.key} className="flex flex-col gap-(--space-3)">
              <span
                className="h-(--space-3) rounded-(--radius-pill)"
                style={{
                  background: reached
                    ? `var(--color-confidence-${rung.token})`
                    : "var(--color-surface-sunken)",
                }}
              />
              <span
                className="flex items-start gap-(--space-2) text-xs leading-tight"
                style={{
                  color: reached
                    ? `var(--color-confidence-${rung.token}-text)`
                    : "var(--color-text-disabled)",
                }}
              >
                {reached ? (
                  <Check className="size-3.5 shrink-0" aria-hidden />
                ) : (
                  <rung.Icon className="size-3.5 shrink-0" aria-hidden />
                )}
                <span>
                  {rung.label}
                  <span className="sr-only">
                    {reached ? " (reached)" : " (not yet)"}
                  </span>
                </span>
              </span>
            </li>
          );
        })}
      </ol>

      {next && (
        <p className="rounded-(--radius-md) bg-(--color-surface-sunken) px-(--space-5) py-(--space-4) text-sm text-(--color-text-muted)">
          <span className="font-semibold text-(--color-text)">
            Next: {next.label}.
          </span>{" "}
          {next.howToReach}
        </p>
      )}
    </div>
  );
}
