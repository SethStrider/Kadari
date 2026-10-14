import type { ButtonHTMLAttributes } from "react";

import { cn } from "@/lib/utils";

const variants = {
  primary:
    "bg-(--color-primary) text-(--color-on-primary) hover:bg-(--color-primary-hover) active:bg-(--color-primary-pressed)",
  secondary:
    "border border-(--color-border-strong) bg-(--color-surface) text-(--color-text) hover:bg-(--color-surface-sunken)",
} as const;

/** KADARI's own button — token-driven, so an insurer white-label re-skins it (the shared ui/Button is Trustli's). */
export function KadariButton({
  variant = "primary",
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: keyof typeof variants;
}) {
  return (
    <button
      className={cn(
        "kd-focus inline-flex h-(--tap-target-min) items-center justify-center gap-(--space-3) rounded-(--radius-md) px-(--space-6) text-sm font-semibold whitespace-nowrap transition-colors duration-(--duration-fast) disabled:pointer-events-none disabled:bg-(--color-control-disabled) disabled:text-(--color-text-disabled) [&_svg]:size-4 [&_svg]:shrink-0",
        variants[variant],
        className,
      )}
      {...props}
    />
  );
}
