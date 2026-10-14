import { type LucideIcon } from "lucide-react";

import { cn } from "@/lib/utils";

/**
 * A single KPI tile, ported from the now-ui-kit stat card and reskinned: a glassy Midnight panel with a
 * brand-tinted icon chip and a trend line. Scaffolding uses static sample figures.
 */
export function StatCard({
  label,
  value,
  delta,
  trend = "up",
  icon: Icon,
}: {
  label: string;
  value: string;
  delta: string;
  trend?: "up" | "down" | "flat";
  icon: LucideIcon;
}) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5 backdrop-blur-md">
      <div className="flex items-start justify-between">
        <div className="flex flex-col gap-1">
          <span className="font-technical text-[11px] tracking-wide text-mid-grey uppercase">
            {label}
          </span>
          <span className="font-heading text-2xl font-bold text-off-white">
            {value}
          </span>
        </div>
        <div className="flex size-11 items-center justify-center rounded-xl bg-gradient-to-br from-[#00DCC2]/20 to-[#095BFF]/20 text-[#00DCC2]">
          <Icon className="size-5" />
        </div>
      </div>
      <div className="mt-4 flex items-center gap-1.5 font-body text-xs">
        <span
          className={cn(
            "font-medium",
            trend === "up" && "text-emerald-400",
            trend === "down" && "text-red-400",
            trend === "flat" && "text-mid-grey",
          )}
        >
          {delta}
        </span>
        <span className="text-mid-grey">vs last week</span>
      </div>
    </div>
  );
}
