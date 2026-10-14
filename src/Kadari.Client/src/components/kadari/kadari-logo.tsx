import { useId } from "react";

/**
 * The KADARI K, traced from the brand sheet: a stem whose foot is cut on a diagonal (dark teal → green), a lower
 * arm (navy → blue) running down-right, and an upper arm (teal → cyan) laid over it, rising to the top-right.
 */
export function KadariMark({ className }: { className?: string }) {
  const id = useId();
  return (
    <svg
      viewBox="0 0 100 92"
      className={className}
      aria-hidden
      focusable="false"
    >
      <defs>
        <linearGradient id={`${id}-stem`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="var(--kd-teal-800)" />
          <stop offset="1" stopColor="var(--kd-mint-400)" />
        </linearGradient>
        <linearGradient id={`${id}-down`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="var(--kd-blue-900)" />
          <stop offset="1" stopColor="var(--kd-blue-400)" />
        </linearGradient>
        <linearGradient id={`${id}-up`} x1="0" y1="1" x2="1" y2="0">
          <stop offset="0" stopColor="var(--kd-teal-600)" />
          <stop offset="1" stopColor="var(--kd-blue-400)" />
        </linearGradient>
      </defs>
      <polygon points="6,6 30,6 30,60 6,88" fill={`url(#${id}-stem)`} />
      <polygon points="33,40 58,40 94,88 66,88" fill={`url(#${id}-down)`} />
      <polygon points="57,6 94,6 48,58 31,41" fill={`url(#${id}-up)`} />
    </svg>
  );
}

/** Mark + spaced wordmark, with the Σ(V × C) formula beneath (the brand lock-up). */
export function KadariLogo({
  formula = true,
  className,
}: {
  formula?: boolean;
  className?: string;
}) {
  return (
    <span
      className={`inline-flex items-center gap-(--space-3) ${className ?? ""}`}
      aria-label="KADARI"
    >
      <KadariMark className="size-9 shrink-0" />
      <span className="flex flex-col leading-none" aria-hidden>
        <span className="text-xl font-semibold tracking-[0.22em]">KADARI</span>
        {formula && (
          <span className="mt-(--space-2) text-xs font-light tracking-[0.3em] opacity-80">
            Σ(V × C)
          </span>
        )}
      </span>
    </span>
  );
}
