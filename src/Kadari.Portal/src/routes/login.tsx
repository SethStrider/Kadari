import { useState } from "react";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Loader2, Lock, Mail } from "lucide-react";

import { localTokenStore } from "@/lib/api";
import { login } from "@/lib/auth/login";

export const Route = createFileRoute("/login")({
  // Already signed in? Skip the form.
  beforeLoad: () => {
    if (localTokenStore.read()) {
      throw redirect({ to: "/dashboard" });
    }
  },
  component: LoginPage,
});

const schema = z.object({
  email: z.string().min(1, "Enter your email").email("Enter a valid email"),
  password: z.string().min(1, "Enter your password"),
});

type LoginForm = z.infer<typeof schema>;

function LoginPage() {
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(schema) });

  const onSubmit = handleSubmit(async ({ email, password }) => {
    setFormError(null);
    const result = await login(email, password);
    if (result.ok) {
      await navigate({ to: "/dashboard" });
    } else {
      setFormError(result.message ?? "Sign in failed.");
    }
  });

  return (
    <div className="relative flex min-h-dvh items-center justify-center overflow-hidden px-6">
      {/* Brand glow behind the card. */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(50% 40% at 50% 38%, rgba(9,91,255,0.22), rgba(0,220,194,0.06) 46%, transparent 72%)",
        }}
      />

      <div className="relative w-full max-w-md">
        <div className="mb-8 flex flex-col items-center text-center">
          <span className="font-heading text-2xl font-semibold tracking-[0.22em] text-off-white">
            KADARI
          </span>
          <p className="mt-4 font-technical text-[11px] tracking-[0.32em] text-mid-grey uppercase">
            Staff Portal
          </p>
        </div>

        <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-8 shadow-[0_24px_60px_-24px_rgba(0,0,0,0.7)] backdrop-blur-xl">
          <h1 className="font-heading text-xl font-semibold text-off-white">
            Sign in
          </h1>
          <p className="mt-1 font-body text-sm text-soft-grey">
            Use your KADARI portal credentials to continue.
          </p>

          <form
            onSubmit={onSubmit}
            className="mt-6 flex flex-col gap-4"
            noValidate
          >
            <label className="flex flex-col gap-1.5">
              <span className="font-heading text-xs font-medium text-soft-grey">
                Email
              </span>
              <div className="relative">
                <Mail className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-mid-grey" />
                <input
                  type="email"
                  autoComplete="username"
                  placeholder="you@kadari.co.za"
                  className="h-11 w-full rounded-xl border border-white/10 bg-trustli-midnight/60 pr-3 pl-10 font-body text-sm text-off-white outline-none transition-colors placeholder:text-mid-grey focus:border-[#00DCC2]/60"
                  {...register("email")}
                />
              </div>
              {errors.email && (
                <span className="font-body text-xs text-red-400">
                  {errors.email.message}
                </span>
              )}
            </label>

            <label className="flex flex-col gap-1.5">
              <span className="font-heading text-xs font-medium text-soft-grey">
                Password
              </span>
              <div className="relative">
                <Lock className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-mid-grey" />
                <input
                  type="password"
                  autoComplete="current-password"
                  placeholder="••••••••"
                  className="h-11 w-full rounded-xl border border-white/10 bg-trustli-midnight/60 pr-3 pl-10 font-body text-sm text-off-white outline-none transition-colors placeholder:text-mid-grey focus:border-[#00DCC2]/60"
                  {...register("password")}
                />
              </div>
              {errors.password && (
                <span className="font-body text-xs text-red-400">
                  {errors.password.message}
                </span>
              )}
            </label>

            {formError && (
              <div
                role="alert"
                className="rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-2 font-body text-xs text-red-300"
              >
                {formError}
              </div>
            )}

            <button
              type="submit"
              disabled={isSubmitting}
              className="mt-2 inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-[#00DCC2] to-[#095BFF] font-heading text-sm font-semibold text-trustli-midnight shadow-[0_4px_18px_-6px_rgba(9,91,255,0.55)] transition-all hover:brightness-110 active:brightness-95 disabled:cursor-not-allowed disabled:opacity-70"
            >
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              {isSubmitting ? "Signing in…" : "Sign in"}
            </button>
          </form>
        </div>

        <p className="mt-6 text-center font-body text-xs text-mid-grey">
          KADARI · Insurer &amp; staff portal
        </p>
      </div>
    </div>
  );
}
