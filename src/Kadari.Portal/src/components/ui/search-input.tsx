import * as React from "react";
import { ArrowRight, Search } from "lucide-react";

import { cn } from "@/lib/utils";

export interface SearchInputProps extends Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  "onSubmit"
> {
  onSubmit?: (value: string) => void;
}

/**
 * Pill-shaped search field: dark navy background, left-aligned Lucide search
 * icon, internal gradient submit-arrow button.
 */
const SearchInput = React.forwardRef<HTMLInputElement, SearchInputProps>(
  ({ className, onSubmit, defaultValue, ...props }, ref) => {
    const [value, setValue] = React.useState(String(defaultValue ?? ""));

    return (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSubmit?.(value);
        }}
        className={cn(
          "group flex h-12 w-full items-center gap-3 rounded-full border border-slate bg-trustli-navy pl-4 pr-1.5",
          "transition-colors focus-within:border-trustli-cyan",
          className,
        )}
      >
        <Search className="size-5 shrink-0 text-mid-grey" />
        <input
          ref={ref}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          className="h-full flex-1 bg-transparent text-sm text-off-white outline-none placeholder:text-mid-grey"
          {...props}
        />
        <button
          type="submit"
          aria-label="Search"
          className="flex size-9 shrink-0 items-center justify-center rounded-full bg-[#1a8a5f] text-white transition-transform hover:scale-105 active:scale-95"
        >
          <ArrowRight className="size-4" />
        </button>
      </form>
    );
  },
);
SearchInput.displayName = "SearchInput";

export { SearchInput };
