/**
 * The API's single error shape (RFC 9457 `application/problem+json`).
 *
 * Every failure the API produces — thrown or framework-generated — arrives in this shape. Branch on
 * `code`, never on `status` or `title`: statuses are coarse (`conflict`, `domain-rule-violated` and
 * `concurrency-conflict` all answer 409) and titles are prose the API may reword.
 */
export interface ApiProblemBody {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  /** The stable machine-readable discriminator. Branch on this. */
  code?: string;
  traceId?: string;
  correlationId?: string;
  /** Present on a 400 from validation: field name → messages. */
  errors?: Record<string, string[]>;
}

/**
 * A failed API call, thrown by the client so TanStack Query error boundaries receive structure
 * rather than a string.
 */
export class ApiProblem extends Error {
  readonly status: number;
  readonly code: string;
  readonly detail?: string;
  readonly traceId?: string;
  readonly correlationId?: string;
  readonly errors?: Record<string, string[]>;

  constructor(body: ApiProblemBody, correlationId?: string) {
    super(body.detail?.trim() || body.title);
    this.name = "ApiProblem";
    this.status = body.status;
    this.code = body.code ?? `http-${body.status}`;
    this.detail = body.detail;
    this.traceId = body.traceId;
    this.correlationId = body.correlationId ?? correlationId;
    this.errors = body.errors;
  }

  /** Field-level messages for a form, or an empty object when this was not a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.errors ?? {};
  }

  /** True when re-issuing the request unchanged could plausibly succeed. */
  get isRetryable(): boolean {
    return this.status === 429 || this.status >= 500;
  }

  /** What to put in a support request. */
  get reference(): string | undefined {
    return this.correlationId ?? this.traceId;
  }
}

/** The API answered, but not with a problem document — a proxy error page, a gateway timeout. */
export function unexpectedResponseProblem(
  status: number,
  correlationId?: string,
): ApiProblem {
  return new ApiProblem(
    {
      type: "urn:trustli:problem:unexpected-response",
      title: "The server returned an unexpected response.",
      status,
      code: "unexpected-response",
    },
    correlationId,
  );
}
