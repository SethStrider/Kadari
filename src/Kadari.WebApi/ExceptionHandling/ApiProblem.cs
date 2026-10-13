using Kadari.Application.Exceptions;
using Kadari.Domain.Abstractions;
using ValidationException = Kadari.Application.Exceptions.ValidationException;

namespace Kadari.WebApi.ExceptionHandling;

/// <summary>
/// One entry in the API's error catalogue: the HTTP status, a stable machine-readable
/// <see cref="Code"/> and a human-readable <see cref="Title"/>.
/// <para>
/// The <see cref="Code"/> — not the status and not the English title — is the contract clients branch
/// on. Statuses are coarse (three different failures all answer 409) and titles are prose that we want
/// to be free to reword or localise, so neither is safe to switch on.
/// </para>
/// </summary>
/// <param name="ExposeMessage">
/// Whether the thrown exception's message may be copied into <c>detail</c> and shown to the caller.
/// True where the message was written for them and describes their own request ("Only an Initiated
/// payment can settle"); false where it describes us — an authorisation decision leaks the permission
/// model, and a server fault leaks table names, hosts and connection strings.
/// </param>
public sealed record ApiProblem(int Status, string Code, string Title, bool ExposeMessage = true)
{
    /// <summary>
    /// RFC 9457 <c>type</c> — the primary identifier of the problem kind.
    /// <para>
    /// A URN rather than an <c>https://</c> link: the spec only requires a URI reference, and a URN
    /// commits us to a stable identifier without promising a documentation page that has to be kept
    /// alive at that exact address. Swap the scheme for a docs URL when one exists — clients that
    /// branch on <see cref="Code"/> are unaffected.
    /// </para>
    /// </summary>
    public string Type { get; } = $"urn:trustli:problem:{Code}";
}

/// <summary>
/// The single catalogue of errors this API can return, and the one place an exception is turned into
/// one. Nothing else in <c>WebApi</c> decides a status code or invents an error string.
/// <para>
/// Mapping lives here rather than on the exceptions themselves so that HTTP stays out of
/// <c>Application</c> and <c>Domain</c> (CLAUDE.md §1 — dependencies point inward).
/// </para>
/// </summary>
public static class ApiProblems
{
    /// <summary>Non-standard but widely understood (nginx): the client hung up before we answered.</summary>
    public const int Status499ClientClosedRequest = 499;

    public static readonly ApiProblem Validation =
        new(StatusCodes.Status400BadRequest, "validation-failed", "One or more validation errors occurred.");

    public static readonly ApiProblem Malformed =
        new(StatusCodes.Status400BadRequest, "malformed-request", "The request could not be read.");

    public static readonly ApiProblem Unauthenticated =
        new(StatusCodes.Status401Unauthorized, "unauthenticated", "Authentication is required.",
            ExposeMessage: false);

    public static readonly ApiProblem Forbidden =
        new(StatusCodes.Status403Forbidden, "forbidden", "You do not have permission to perform this action.",
            ExposeMessage: false);

    // A distinct code (not "forbidden") so the client knows to show the consent screen; the message names the scope.
    public static readonly ApiProblem ConsentRequired =
        new(StatusCodes.Status403Forbidden, "consent-required", "The customer has not given the consent this action needs.");

    public static readonly ApiProblem OtpRejected =
        new(StatusCodes.Status403Forbidden, "otp-rejected", "The verification code was rejected.");

    // 401 (not 403): the caller hasn't proven who they are. The message is deliberately generic so the
    // response is identical whether the email is unknown, the password wrong, or the account locked.
    public static readonly ApiProblem InvalidCredentials =
        new(StatusCodes.Status401Unauthorized, "invalid-credentials", "Invalid email or password.");

    public static readonly ApiProblem NotFound =
        new(StatusCodes.Status404NotFound, "not-found", "Resource not found.");

    public static readonly ApiProblem MethodNotAllowed =
        new(StatusCodes.Status405MethodNotAllowed, "method-not-allowed", "That method is not allowed on this resource.");

    public static readonly ApiProblem Conflict =
        new(StatusCodes.Status409Conflict, "conflict", "The request conflicts with the current state.");

    public static readonly ApiProblem DomainRuleViolated =
        new(StatusCodes.Status409Conflict, "domain-rule-violated", "The operation is not allowed in the current state.");

    public static readonly ApiProblem ConcurrencyConflict =
        new(StatusCodes.Status409Conflict, "concurrency-conflict", "The resource was modified by another request.");

    public static readonly ApiProblem UnsupportedMediaType =
        new(StatusCodes.Status415UnsupportedMediaType, "unsupported-media-type", "That content type is not supported.");

    public static readonly ApiProblem PayloadTooLarge =
        new(StatusCodes.Status413PayloadTooLarge, "payload-too-large", "The request body is too large.");

    public static readonly ApiProblem TooManyRequests =
        new(StatusCodes.Status429TooManyRequests, "too-many-requests", "Too many requests. Retry later.");

    public static readonly ApiProblem ClientClosedRequest =
        new(Status499ClientClosedRequest, "request-cancelled", "The client closed the request.",
            ExposeMessage: false);

    public static readonly ApiProblem Internal =
        new(StatusCodes.Status500InternalServerError, "internal-error", "An unexpected error occurred.",
            ExposeMessage: false);

    public static readonly ApiProblem Unavailable =
        new(StatusCodes.Status503ServiceUnavailable, "service-unavailable", "The service is temporarily unavailable.",
            ExposeMessage: false);

    /// <summary>
    /// Maps an exception to its catalogue entry. Anything unrecognised is deliberately
    /// <see cref="Internal"/> — an unmapped exception is a bug, and a bug is a 500.
    /// </summary>
    /// <remarks>
    /// Order matters: the most-derived types are matched first (<see cref="OtpRejectedException"/>
    /// derives from <see cref="ForbiddenException"/>).
    /// </remarks>
    public static ApiProblem For(Exception exception) => exception switch
    {
        ValidationException => Validation,
        NotFoundException => NotFound,
        ConflictException => Conflict,
        TooManyRequestsException => TooManyRequests,
        ConcurrencyException => ConcurrencyConflict,
        OtpRejectedException => OtpRejected,
        ConsentRequiredException => ConsentRequired,
        InvalidCredentialsException => InvalidCredentials,
        ForbiddenException => Forbidden,

        // A broken invariant is a state conflict, not a server fault. Without this the ~40
        // aggregate guards in Domain all answered 500.
        DomainException => DomainRuleViolated,

        // Unreadable JSON, an unbindable route value, an oversized body. Kestrel/minimal-API
        // binding raise these and carry the status they intend.
        BadHttpRequestException bad => FromStatus(bad.StatusCode),

        // Raised by the framework's authorization plumbing rather than our own filters.
        UnauthorizedAccessException => Forbidden,

        _ => Internal,
    };

    /// <summary>
    /// The catalogue entry for a bare status code, used for responses the framework produces without
    /// an exception (no route matched, no credentials, wrong content type) so that <em>every</em> error
    /// this API emits carries a <c>code</c>.
    /// </summary>
    public static ApiProblem FromStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => Malformed,
        StatusCodes.Status401Unauthorized => Unauthenticated,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status413PayloadTooLarge => PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        StatusCodes.Status429TooManyRequests => TooManyRequests,
        StatusCodes.Status503ServiceUnavailable => Unavailable,
        Status499ClientClosedRequest => ClientClosedRequest,
        >= 500 => Internal,

        // An unlisted 4xx: still give it a code so clients never have to special-case its absence.
        _ => new ApiProblem(status, $"http-{status}", "The request could not be completed."),
    };
}
