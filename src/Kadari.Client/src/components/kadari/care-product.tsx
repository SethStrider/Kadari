import {
  BatteryCharging,
  Drill,
  Home,
  Laptop,
  Microwave,
  Package,
  Smartphone,
  Tv,
  WashingMachine,
  type LucideIcon,
} from "lucide-react";

import { cn } from "@/lib/utils";

/** What the product header shows: the owner's name for it, and the brand, model and room KADARI knows. */
export interface CareProduct {
  name: string;
  brand?: string | null;
  model?: string | null;
  room?: string | null;
  careCategory?: string | null;
}

const categoryIcons: Record<string, LucideIcon> = {
  MajorAppliance: WashingMachine,
  SmallAppliance: Microwave,
  TvAndAudio: Tv,
  Mobile: Smartphone,
  Computing: Laptop,
  PowerTools: Drill,
  PortablePower: BatteryCharging,
};

/**
 * The product at the top of the Care screens (design board 2): a picture tile, the brand, the product and its model
 * code, and the room. KADARI keeps no product photo it can show here, so the tile carries the category's icon.
 */
export function CareProductHeader({
  product,
  compact = false,
}: {
  product: CareProduct;
  compact?: boolean;
}) {
  const Icon = categoryIcons[product.careCategory ?? ""] ?? Package;
  const showName =
    !product.brand ||
    !product.name.toLowerCase().startsWith(product.brand.toLowerCase());

  return (
    <div className="flex items-center gap-(--space-5) rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-5) shadow-(--shadow-1)">
      <span
        className={cn(
          "flex shrink-0 items-center justify-center rounded-(--radius-md) bg-(--color-surface-sunken) text-(--color-text-muted)",
          compact ? "size-14" : "size-20",
        )}
      >
        <Icon className={compact ? "size-7" : "size-10"} aria-hidden />
      </span>
      <div className="flex min-w-0 flex-col gap-(--space-1)">
        {product.brand && <p className="font-semibold">{product.brand}</p>}
        <p className={cn("text-sm", !product.brand && "font-semibold")}>
          {showName
            ? product.name
            : product.name.slice(product.brand!.length).trim() || product.name}
        </p>
        {product.model && (
          <p className="text-xs text-(--color-text-muted)">{product.model}</p>
        )}
        {product.room && !compact && (
          <span className="mt-(--space-1) inline-flex items-center gap-(--space-2) self-start rounded-(--radius-pill) border border-(--color-border) px-(--space-3) py-(--space-1) text-xs text-(--color-text-muted)">
            <Home className="size-3.5" aria-hidden />
            Home · {product.room}
          </span>
        )}
      </div>
    </div>
  );
}
