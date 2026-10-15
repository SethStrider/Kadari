import { apiClient } from "./client";
import { apiBaseUrl } from "./raw";

/**
 * Customer auth — the OTP flow (mobile → code → JWT, which auto-registers a Customer). Raw `fetch` (the
 * typed client doesn't cover these yet); the verified tokens are handed to {@link ApiClient} so every other
 * call carries the bearer and refreshes on 401.
 */

export interface CurrentCustomer {
  customerId: string;
  firstName: string;
  lastName: string;
  mobileNumber: string;
}

/** The refresh token is set in an HttpOnly cookie (GL-16), so only the access token comes back. */
interface VerifyOtpBody {
  accessToken: string;
  expiresAt: string;
}

export async function requestOtp(mobileNumber: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl()}/api/v1/auth/otp/request`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    credentials: "include",
    body: JSON.stringify({ mobileNumber }),
  });
  if (!response.ok) {
    throw new Error(`Could not send the code (${response.status}).`);
  }
}

/** Verify the code and store the resulting tokens. */
export async function verifyOtp(
  mobileNumber: string,
  code: string,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl()}/api/v1/auth/otp/verify`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    credentials: "include",
    body: JSON.stringify({ mobileNumber, code }),
  });
  if (!response.ok) {
    throw new Error(`Invalid or expired code (${response.status}).`);
  }

  const body = (await response.json()) as VerifyOtpBody;
  apiClient.setTokens({
    accessToken: body.accessToken,
    expiresAt: body.expiresAt,
  });
}

/** The signed-in customer's own id + profile (resolves the token to a customer id). */
export async function getCurrentCustomer(): Promise<CurrentCustomer> {
  const response = await apiClient.fetch("/api/v1/customers/me", {
    method: "GET",
    headers: { Accept: "application/json" },
  });
  if (!response.ok) {
    throw new Error(`Could not resolve your account (${response.status}).`);
  }
  return (await response.json()) as CurrentCustomer;
}
