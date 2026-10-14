import { createContext, useContext, useEffect, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";

import { getInsurerContext, type InsurerContext } from "@/lib/api/insurer";

/**
 * White-label theming for the insurer console. On sign-in it resolves the caller's own tenant context
 * (`GET /insurer/context`); when that tenant carries branding, the tenant's colours are written onto the
 * document root as CSS-variable overrides, re-skinning the shadcn `--primary` / `--accent` / `--ring`
 * tokens the whole console is built on. A plain staff user (no insurer binding) gets a 404 — no context,
 * no override, the default Trustli palette stands. The resolved context is also exposed so the shell can
 * show the tenant's own logo and name.
 */

interface InsurerThemeValue {
  context: InsurerContext | null;
  isInsurer: boolean;
  isLoading: boolean;
}

const InsurerThemeContext = createContext<InsurerThemeValue>({
  context: null,
  isInsurer: false,
  isLoading: false,
});

const BRAND_VARS = ["--primary", "--accent", "--ring"] as const;

export function InsurerThemeProvider({ children }: { children: ReactNode }) {
  const { data, isLoading } = useQuery({
    queryKey: ["insurer-context"],
    queryFn: () => getInsurerContext(),
    retry: false, // a non-insurer staff user gets a 404 — don't hammer the endpoint
    staleTime: 5 * 60_000,
  });

  const context = data ?? null;
  const primary = context?.branding?.primaryColor;
  const accent = context?.branding?.accentColor;

  useEffect(() => {
    const root = document.documentElement;
    if (!primary && !accent) return;

    if (primary) root.style.setProperty("--primary", primary);
    if (accent) {
      root.style.setProperty("--accent", accent);
      root.style.setProperty("--ring", accent);
    }

    return () => {
      for (const v of BRAND_VARS) root.style.removeProperty(v);
    };
  }, [primary, accent]);

  return (
    <InsurerThemeContext.Provider
      value={{ context, isInsurer: context !== null, isLoading }}
    >
      {children}
    </InsurerThemeContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useInsurerContext(): InsurerThemeValue {
  return useContext(InsurerThemeContext);
}
