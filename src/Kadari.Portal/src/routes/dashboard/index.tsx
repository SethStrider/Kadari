import { useEffect } from "react";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { Loader2 } from "lucide-react";

import { useInsurerContext } from "@/lib/theme/insurer-theme";

export const Route = createFileRoute("/dashboard/")({
  component: DashboardHome,
});

/** The portal's home is the caller's main page: an insurer's portfolio, or user management for staff. */
function DashboardHome() {
  const navigate = useNavigate();
  const { isInsurer, isLoading } = useInsurerContext();

  useEffect(() => {
    if (isLoading) return;
    void navigate({
      to: isInsurer ? "/dashboard/portfolio" : "/dashboard/users",
      replace: true,
    });
  }, [isInsurer, isLoading, navigate]);

  return (
    <div className="grid flex-1 place-items-center">
      <Loader2 className="size-6 animate-spin" />
    </div>
  );
}
