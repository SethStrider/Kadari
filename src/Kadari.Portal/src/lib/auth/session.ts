import { create } from "zustand";

import { apiClient, localTokenStore, type TokenPair } from "@/lib/api";

/**
 * The signed-in staff member, as far as the client knows it — decoded from the access token's claims.
 * The server remains the authority (it re-checks the session on every call); this is only for chrome
 * (greeting, role-aware menus).
 */
export interface AuthUser {
  id: string;
  name: string;
  roles: string[];
}

interface SessionState {
  user: AuthUser | null;
  isAuthenticated: boolean;
  /** Persist a freshly issued token pair and adopt its identity. */
  setFromTokens: (tokens: TokenPair) => void;
  /** Read whatever is in storage on app start and reflect it. */
  hydrate: () => void;
  signOut: () => void;
}

const ROLE_CLAIMS = [
  "role",
  "roles",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
];

/** Decode a JWT payload without verifying it — the server verifies; the client only reads claims. */
function decode(accessToken: string): AuthUser | null {
  try {
    const payload = accessToken.split(".")[1];
    if (!payload) return null;

    const json = JSON.parse(
      atob(payload.replace(/-/g, "+").replace(/_/g, "/")),
    ) as Record<string, unknown>;

    const rawRole = ROLE_CLAIMS.map((claim) => json[claim]).find(
      (v) => v !== undefined,
    );
    const roles = Array.isArray(rawRole)
      ? (rawRole as string[])
      : typeof rawRole === "string"
        ? [rawRole]
        : [];

    return {
      id: typeof json.sub === "string" ? json.sub : "",
      name: typeof json.name === "string" ? json.name : "",
      roles,
    };
  } catch {
    return null;
  }
}

export const useSession = create<SessionState>((set) => ({
  user: null,
  isAuthenticated: false,

  setFromTokens: (tokens) => {
    localTokenStore.write(tokens);
    const user = decode(tokens.accessToken);
    set({ user, isAuthenticated: user !== null });
  },

  hydrate: () => {
    const stored = localTokenStore.read();
    const user = stored ? decode(stored.accessToken) : null;
    set({ user, isAuthenticated: user !== null });
  },

  signOut: () => {
    // Ends the session server-side and clears the HttpOnly refresh cookie as well as the stored access token
    // (GL-16); clearing local storage alone would leave the session alive behind the cookie.
    apiClient.signOut();
    set({ user: null, isAuthenticated: false });
  },
}));
