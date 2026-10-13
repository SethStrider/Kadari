using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Kadari.Application.Exceptions;
using ValidationException = Kadari.Application.Exceptions.ValidationException;

namespace Kadari.WebApi.ExceptionHandling;

/// <summary>
/// The single edge that turns an exception into an RFC 9457 <c>application/problem+json</c> response
/// (API design standard, #3). Endpoints never build error bodies by hand (CLAUDE.md §9).
/// <para>
/// Status, code and title come from <see cref="ApiProblems"/>; this class owns only the three things
/// that need the request: how much to say in <c>detail</c>, what to log and at what level, and how to
/// behave when the response can no longer be written. Fields common to <em>every</em> problem
/// (<c>code</c>, <c>traceId</c>, <c>correlationId</c>, <c>instance</c>) are stamped by
/// <see cref="ProblemDetailsCustomizer"/>, which also covers responses the framework produces without
/// an exception.
/// </para>
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        // The client hung up. There is nobody to answer and writing to a dead connection throws, so
        // record the outcome and stop — this is not a server fault and must not page anyone.
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request {Method} {Path} was aborted by the client.",
                context.Request.Method, context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ApiProblems.ClientClosedRequest.Status;
            }

            return true;
        }

        var problem = ApiProblems.For(exception);

        Log(context, exception, problem);

        // Bytes are already on the wire: the status line is spent and any body we add would corrupt
        // the payload. Returning false lets the host abort the connection, which is the honest signal
        // to the client that the response is incomplete.
        if (context.Response.HasStarted)
        {
            logger.LogWarning(
                "Response for {Method} {Path} had already started; could not write a problem response.",
                context.Request.Method, context.Request.Path);

            return false;
        }

        context.Response.StatusCode = problem.Status;

        var problemDetails = new ProblemDetails
        {
            Status = problem.Status,
            Type = problem.Type,
            Title = problem.Title,
            Detail = Detail(exception, problem),
        };

        // The catalogue distinguishes failures the status code cannot (conflict vs. domain-rule vs.
        // concurrency all answer 409), so the handler owns `code`; the customizer only fills it in
        // for problems that arrive without one.
        problemDetails.Extensions["code"] = problem.Code;

        Enrich(problemDetails, exception);

        var problemContext = new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problemDetails,
        };

        if (await problemDetailsService.TryWriteAsync(problemContext))
        {
            return true;
        }

        // No writer accepted the response (e.g. the client's Accept header excludes JSON). An empty
        // body is worse than an unrequested content type, so write the problem ourselves.
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", ct);

        return true;
    }

    /// <summary>
    /// What the caller is told, governed by <see cref="ApiProblem.ExposeMessage"/> — the catalogue
    /// decides which failures may quote their exception. Withheld messages are still logged, and in
    /// Development they are returned anyway, because there the developer <em>is</em> the caller.
    /// </summary>
    private string? Detail(Exception exception, ApiProblem problem)
    {
        if (problem.ExposeMessage)
        {
            // The validation message is the same sentence as the title; the per-field `errors`
            // extension is the useful payload, so don't repeat ourselves.
            return exception is ValidationException ? null : exception.Message;
        }

        return environment.IsDevelopment()
            ? $"{exception.GetType().Name}: {exception.Message}"
            : problem.Title;
    }

    /// <summary>Adds the fields that only a specific kind of failure has.</summary>
    private void Enrich(ProblemDetails problemDetails, Exception exception)
    {
        switch (exception)
        {
            // The shape ASP.NET Core uses for validation everywhere else, so existing clients and
            // generated SDKs read it without special-casing Trustli.
            case ValidationException validation:
                problemDetails.Extensions["errors"] = validation.Errors;
                break;

            // Lets a client distinguish "no such journey" from "no such document" without parsing prose.
            case NotFoundException notFound:
                problemDetails.Extensions["resource"] = notFound.Resource;
                break;
        }

        // Development only: the stack trace is the single most useful thing when debugging against
        // Scalar, and the single most dangerous thing to leak in production.
        if (environment.IsDevelopment() && exception.StackTrace is { } stackTrace)
        {
            problemDetails.Extensions["stackTrace"] = stackTrace;
        }
    }

    /// <summary>
    /// Severity follows who is at fault. A 500 is our bug and must be loud, with the stack. A 401/403
    /// is a security signal worth seeing but is not an error — and logging its stack on every probe
    /// would bury the real failures. Remaining client errors are routine and stay at Debug.
    /// </summary>
    private void Log(HttpContext context, Exception exception, ApiProblem problem)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} → {StatusCode} {ErrorCode}.",
                method, path, problem.Status, problem.Code);
        }
        else if (problem.Status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            logger.LogWarning(
                "Access denied for {Method} {Path} → {StatusCode} {ErrorCode}: {Reason}",
                method, path, problem.Status, problem.Code, exception.Message);
        }
        else
        {
            logger.LogDebug(
                exception,
                "Client error for {Method} {Path} → {StatusCode} {ErrorCode}.",
                method, path, problem.Status, problem.Code);
        }
    }
}
