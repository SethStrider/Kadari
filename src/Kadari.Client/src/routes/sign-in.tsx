import { useState } from "react";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";

import { KadariLogo } from "@/components/kadari/kadari-logo";
import { Button } from "@/components/ui/button";
import { requestOtp, verifyOtp, getCurrentCustomer } from "@/lib/api/auth";

export const Route = createFileRoute("/sign-in")({
  component: SignInPage,
});

type Step = "mobile" | "code";

function SignInPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState<Step>("mobile");
  const [mobile, setMobile] = useState("");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);

  async function sendCode() {
    setBusy(true);
    try {
      await requestOtp(mobile.trim());
      setStep("code");
      toast.success(
        "Code sent.",
      );
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not send the code.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function verify() {
    setBusy(true);
    try {
      await verifyOtp(mobile.trim(), code.trim());
      const me = await getCurrentCustomer();
      void navigate({
        to: "/$customerId/ledger",
        params: { customerId: me.customerId },
      });
    } catch (cause) {
      toast.error(
        cause instanceof Error ? cause.message : "Could not sign in.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="kadari flex min-h-svh flex-col items-center justify-center px-6 py-16">
    <div className="flex w-full max-w-sm flex-col gap-6">
      <KadariLogo />
      <div>
        <h1 className="text-2xl font-semibold">Sign in</h1>
        <p className="mt-1 text-sm text-(--color-text-muted)">
          {step === "mobile"
            ? "Enter your mobile number to receive a one-time code."
            : `Enter the code sent to ${mobile}.`}
        </p>
      </div>

      {step === "mobile" ? (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void sendCode();
          }}
          className="flex flex-col gap-3"
        >
          <input
            value={mobile}
            onChange={(event) => setMobile(event.target.value)}
            placeholder="+27 82 000 0000"
            autoComplete="tel"
            className="h-11 rounded-xl border border-(--color-border) bg-(--color-surface) px-4 text-sm placeholder:text-(--color-text-muted)"
          />
          <Button
            type="submit"
            disabled={busy || mobile.trim().length < 6}
            className="h-11"
          >
            {busy && <Loader2 className="size-4 animate-spin" />} Send code
          </Button>
        </form>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void verify();
          }}
          className="flex flex-col gap-3"
        >
          <input
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="6-digit code"
            inputMode="numeric"
            autoComplete="one-time-code"
            className="h-11 rounded-xl border border-(--color-border) bg-(--color-surface) px-4 text-center text-lg tracking-widest placeholder:text-(--color-text-muted)"
          />
          <Button
            type="submit"
            disabled={busy || code.trim().length < 4}
            className="h-11"
          >
            {busy && <Loader2 className="size-4 animate-spin" />} Verify &amp;
            continue
          </Button>
          <button
            type="button"
            onClick={() => setStep("mobile")}
            className="text-center text-xs text-(--color-text-muted) hover:underline"
          >
            Use a different number
          </button>
        </form>
      )}
    </div>
    </div>
  );
}
