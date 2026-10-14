import { apiClient, ApiProblem } from "@/lib/api";

import { useSession } from "./session";

/**
 * Staff sign-in. Delegates the request to the typed API client (which owns the one sanctioned `fetch`),
 * then adopts the issued tokens into the session. Never surfaces which check failed — the server returns
 * a single generic reason and the form mirrors that.
 */
export interface LoginResult {
  ok: boolean;
  /** A user-safe message for the form when {@link ok} is false. */
  message?: string;
}

export async function login(
  email: string,
  password: string,
): Promise<LoginResult> {
  try {
    const tokens = await apiClient.passwordLogin(email, password);
    useSession.getState().setFromTokens(tokens);
    return { ok: true };
  } catch (error) {
    if (error instanceof ApiProblem) {
      if (error.status === 429) {
        return {
          ok: false,
          message:
            "Too many attempts. Please wait a few minutes and try again.",
        };
      }
      // 401 invalid-credentials — the server returns one generic reason on purpose.
      return { ok: false, message: "Invalid email or password." };
    }

    return {
      ok: false,
      message:
        "Could not reach the server. Check your connection and try again.",
    };
  }
}

export function logout(): void {
  useSession.getState().signOut();
}
