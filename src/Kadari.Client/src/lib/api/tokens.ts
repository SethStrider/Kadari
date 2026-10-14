/**
 * Where the access and refresh tokens live between requests.
 *
 * Access tokens are short-lived (15 minutes) and the session behind them is checked server-side on
 * every call, so a terminated session stops working within seconds rather than when the token
 * happens to expire.
 */
export interface TokenPair {
  accessToken: string;
  expiresAt: string;
  /**
   * Only on a session signed in before GL-16: the refresh token now lives in an HttpOnly cookie that no script
   * can read. A leftover one is sent once on the next refresh so that browser moves to the cookie, then dropped.
   */
  refreshToken?: string;
}

const STORAGE_KEY = "kadari.tokens";

/**
 * Token storage.
 *
 * `localStorage` is readable by any script on the origin, so it is only as safe as the app's XSS
 * posture. It is behind this interface precisely so moving to httpOnly cookies later touches one
 * file rather than every call site.
 */
export interface TokenStore {
  read(): TokenPair | null;
  write(tokens: TokenPair): void;
  clear(): void;
}

export const localTokenStore: TokenStore = {
  read() {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    try {
      return JSON.parse(raw) as TokenPair;
    } catch {
      // A malformed entry is indistinguishable from no session; do not let it wedge sign-in.
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
  },

  write(tokens) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));
  },

  clear() {
    localStorage.removeItem(STORAGE_KEY);
  },
};
