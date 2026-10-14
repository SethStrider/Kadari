import { useEffect } from "react";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { Loader2 } from "lucide-react";

import { getCurrentCustomer } from "@/lib/api/auth";
import { localTokenStore } from "@/lib/api/tokens";

export const Route = createFileRoute("/")({
  component: Home,
});

/** Signed in: straight to the ledger. Otherwise: sign in. */
function Home() {
  const navigate = useNavigate();

  useEffect(() => {
    if (!localTokenStore.read()) {
      void navigate({ to: "/sign-in", replace: true });
      return;
    }

    getCurrentCustomer()
      .then((me) =>
        navigate({
          to: "/$customerId/ledger",
          params: { customerId: me.customerId },
          replace: true,
        }),
      )
      .catch(() => navigate({ to: "/sign-in", replace: true }));
  }, [navigate]);

  return (
    <div className="kadari grid min-h-svh place-items-center">
      <Loader2 className="size-6 animate-spin" />
    </div>
  );
}
