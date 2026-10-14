import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";

import { CaptureCamera } from "@/components/kadari/capture-camera";
import {
  CaptureConsent,
  ConsentSettings,
} from "@/components/kadari/capture-consent";
import { KadariShell } from "@/components/kadari/kadari-shell";
import { getConsents } from "@/lib/api/kadari";

export const Route = createFileRoute("/$customerId/capture")({
  component: CapturePage,
});

function CapturePage() {
  const { customerId } = Route.useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const consentsKey = ["kadari", "consents", customerId];
  const {
    data: consents,
    isPending,
    isError,
  } = useQuery({
    queryKey: consentsKey,
    queryFn: ({ signal }) => getConsents(customerId, signal),
  });
  const refreshConsents = () =>
    void queryClient.invalidateQueries({ queryKey: consentsKey });

  // The camera only opens once the customer has agreed to image processing — the server refuses otherwise.
  const mayCapture =
    consents?.find((c) => c.scope === "AssetImageProcessing")?.granted ?? false;

  return (
    <KadariShell
      customerId={customerId}
      title="Capture"
      subtitle="Walk the room and capture what you own."
    >
      {isPending && (
        <div className="flex items-center gap-(--space-3) text-sm text-(--color-text-muted)">
          <Loader2 className="size-4 animate-spin" aria-hidden /> Loading…
        </div>
      )}

      {isError && (
        <p className="text-sm text-(--color-danger)">
          We couldn’t load your privacy choices. Please try again.
        </p>
      )}

      {consents && !mayCapture && (
        <CaptureConsent customerId={customerId} onGranted={refreshConsents} />
      )}

      {consents && mayCapture && (
        <>
          <CaptureCamera
            customerId={customerId}
            onConsentRequired={refreshConsents}
            onCaptured={() =>
              void navigate({
                to: "/$customerId/ledger",
                params: { customerId },
              })
            }
          />
          <ConsentSettings
            customerId={customerId}
            consents={consents}
            onChanged={refreshConsents}
          />
        </>
      )}
    </KadariShell>
  );
}
