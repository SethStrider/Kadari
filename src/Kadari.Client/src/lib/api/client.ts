import type { paths } from "./schema";
import {
  ApiProblem,
  unexpectedResponseProblem,
  type ApiProblemBody,
} from "./problem";
import { localTokenStore, type TokenPair, type TokenStore } from "./tokens";

/**
 * The only place in the client that calls `fetch` (PAS-002 §6).
 *
 * Typed against `schema.d.ts`, which is generated from the committed `openapi/v1.json`. A backend
 * contract change therefore becomes a client compile error rather than a runtime surprise — which is
 * the entire point of establishing this before the UI is built.
 *
 * Responsibilities kept here so no feature has to remember them:
 *  - attach the bearer token;
 *  - refresh once on 401, then sign out;
 *  - send a correlation id on every request;
 *  - turn `application/problem+json` into a typed {@link ApiProblem}.
 */

export const CORRELATION_HEADER = "X-Correlation-ID";

type Paths = paths;
type PathOf<M extends string> = {
  [P in keyof Paths]: M extends keyof Paths[P]
    ? Paths[P][M] extends undefined | never
      ? never
      : P
    : never;
}[keyof Paths];

/** Extracts the 2xx response body type for an operation, or `void` when it returns no content. */
type SuccessBody<T> = T extends { responses: infer R }
  ? R extends Record<string, unknown>
    ? {
        [S in keyof R]: S extends 200 | 201 | 202
          ? R[S] extends { content: { "application/json": infer B } }
            ? B
            : void
          : never;
      }[keyof R]
    : void
  : void;

export interface ApiClientOptions {
  baseUrl?: string;
  tokens?: TokenStore;
  /** Called when refresh fails and the session is over — route to sign-in from here. */
  onSignedOut?: () => void;
}

export interface RequestOptions {
  path?: Record<string, string | number>;
  query?: Record<string, string | number | boolean | undefined>;
  body?: unknown;
  signal?: AbortSignal;
  /** Skip the bearer token — the auth endpoints, which have no session yet. */
  anonymous?: boolean;
}

function resolveBaseUrl(explicit?: string): string {
  const configured =
    explicit ?? (import.meta.env.VITE_API_BASE_URL as string | undefined);

  // Undefined means nobody configured it — refuse to guess: CORS is origin-pinned server-side, so a
  // wrong guess fails at preflight with nothing useful in the console.
  if (configured === undefined) {
    throw new Error(
      "VITE_API_BASE_URL is not set. The client cannot guess the API origin — CORS is origin-pinned " +
        "server-side, so a wrong guess fails at preflight with nothing useful in the console.",
    );
  }

  // An explicit empty string is a deliberate choice: "same origin". The container image serves the SPA
  // behind nginx, which reverse-proxies /api → the Web API, so requests hit a relative /api/... on the
  // page's own origin. buildUrl prepends this, so "" yields "/api/...". Dev sets an absolute origin.
  return configured.replace(/\/$/, "");
}

function buildUrl(
  baseUrl: string,
  template: string,
  path?: RequestOptions["path"],
  query?: RequestOptions["query"],
): string {
  const withPath = template.replace(/\{(\w+)\}/g, (_, key: string) => {
    const value = path?.[key];
    if (value === undefined) {
      throw new Error(`Missing path parameter "${key}" for ${template}.`);
    }
    return encodeURIComponent(String(value));
  });

  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined) search.append(key, String(value));
  }

  const qs = search.toString();
  return `${baseUrl}${withPath}${qs ? `?${qs}` : ""}`;
}

export class ApiClient {
  private readonly baseUrl: string;
  private readonly tokens: TokenStore;
  private readonly onSignedOut?: () => void;

  /** In-flight refresh, shared so concurrent 401s trigger exactly one refresh. */
  private refreshing: Promise<TokenPair | null> | null = null;

  constructor(options: ApiClientOptions = {}) {
    this.baseUrl = resolveBaseUrl(options.baseUrl);
    this.tokens = options.tokens ?? localTokenStore;
    this.onSignedOut = options.onSignedOut;
  }

  get<P extends PathOf<"get">>(
    path: P,
    options?: RequestOptions,
  ): Promise<SuccessBody<Paths[P] extends { get: infer O } ? O : never>> {
    return this.send("GET", path as string, options);
  }

  post<P extends PathOf<"post">>(
    path: P,
    options?: RequestOptions,
  ): Promise<SuccessBody<Paths[P] extends { post: infer O } ? O : never>> {
    return this.send("POST", path as string, options);
  }

  put<P extends PathOf<"put">>(
    path: P,
    options?: RequestOptions,
  ): Promise<SuccessBody<Paths[P] extends { put: infer O } ? O : never>> {
    return this.send("PUT", path as string, options);
  }

  delete<P extends PathOf<"delete">>(
    path: P,
    options?: RequestOptions,
  ): Promise<SuccessBody<Paths[P] extends { delete: infer O } ? O : never>> {
    return this.send("DELETE", path as string, options);
  }

  setTokens(tokens: TokenPair): void {
    this.tokens.write(tokens);
  }

  signOut(): void {
    // End the session server-side and clear the HttpOnly refresh cookie (GL-16). Best effort: the local token
    // is cleared regardless, and the session also ends on its own at expiry.
    void fetch(`${this.baseUrl}/api/v1/auth/logout`, {
      method: "POST",
      credentials: "include",
    }).catch(() => undefined);
    this.tokens.clear();
    this.onSignedOut?.();
  }

  private async send<T>(
    method: string,
    template: string,
    options: RequestOptions = {},
  ): Promise<T> {
    const correlationId = crypto.randomUUID();

    let response = await this.dispatch(
      method,
      template,
      options,
      correlationId,
    );

    // One refresh attempt, then the session is over. Retrying further would just loop.
    if (response.status === 401 && !options.anonymous) {
      const refreshed = await this.refreshOnce();

      if (!refreshed) {
        this.signOut();
        throw await this.toProblem(response, correlationId);
      }

      response = await this.dispatch(method, template, options, correlationId);
    }

    if (!response.ok) {
      throw await this.toProblem(response, correlationId);
    }

    if (
      response.status === 204 ||
      response.headers.get("content-length") === "0"
    ) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  private dispatch(
    method: string,
    template: string,
    options: RequestOptions,
    correlationId: string,
  ): Promise<Response> {
    const headers: Record<string, string> = {
      Accept: "application/json",
      [CORRELATION_HEADER]: correlationId,
    };

    if (options.body !== undefined) {
      headers["Content-Type"] = "application/json";
    }

    if (!options.anonymous) {
      const accessToken = this.tokens.read()?.accessToken;
      if (accessToken) {
        headers.Authorization = `Bearer ${accessToken}`;
      }
    }

    return fetch(
      buildUrl(this.baseUrl, template, options.path, options.query),
      {
        method,
        headers,
        credentials: "include",
        signal: options.signal,
        body:
          options.body === undefined ? undefined : JSON.stringify(options.body),
      },
    );
  }

  /** Refreshes at most once concurrently; parallel 401s all await the same attempt. */
  private refreshOnce(): Promise<TokenPair | null> {
    this.refreshing ??= this.performRefresh().finally(() => {
      this.refreshing = null;
    });

    return this.refreshing;
  }

  private async performRefresh(): Promise<TokenPair | null> {
    // Signed out: nothing to refresh. Signed in: the HttpOnly cookie carries the refresh token (GL-16); a
    // pre-cookie token still in storage is sent once so the server can move this browser onto the cookie.
    const stored = this.tokens.read();
    if (!stored) return null;
    const legacyRefreshToken = stored.refreshToken;

    const response = await fetch(`${this.baseUrl}/api/v1/auth/refresh`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      credentials: "include",
      body: JSON.stringify(
        legacyRefreshToken ? { refreshToken: legacyRefreshToken } : {},
      ),
    });

    if (!response.ok) return null;

    // The refresh token rotated in the cookie; only the new access token comes back in the body.
    const body = (await response.json()) as {
      accessToken: string;
      expiresAt: string;
    };

    const tokens: TokenPair = {
      accessToken: body.accessToken,
      expiresAt: body.expiresAt,
    };

    this.tokens.write(tokens);
    return tokens;
  }

  private async toProblem(
    response: Response,
    correlationId: string,
  ): Promise<ApiProblem> {
    const contentType = response.headers.get("content-type") ?? "";

    if (
      !contentType.includes("problem+json") &&
      !contentType.includes("application/json")
    ) {
      return unexpectedResponseProblem(response.status, correlationId);
    }

    try {
      return new ApiProblem(
        (await response.json()) as ApiProblemBody,
        correlationId,
      );
    } catch {
      return unexpectedResponseProblem(response.status, correlationId);
    }
  }
}

export const apiClient = new ApiClient();
