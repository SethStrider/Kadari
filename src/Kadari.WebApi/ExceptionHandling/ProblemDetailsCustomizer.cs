using System.Diagnostics;
using Kadari.WebApi.Middleware;

namespace Kadari.WebApi.ExceptionHandling;

/// <summary>
/// Stamps the fields every problem response carries, whatever produced it. Wired once as
/// <c>ProblemDetailsOptions.CustomizeProblemDetails</c>, so it runs for
/// <see cref="GlobalExceptionHandler"/>, for <c>Results.Problem/ValidationProblem</c> from an endpoint,
/// and — via <c>UseStatusCodePages</c> — for bare status codes the framework returns with no exception
/// at all (no route matched, no credentials, unsupported content type).
/// <para>
/// Doing it here rather than in the exception handler is what makes the error contract uniform: a 404
/// from a missing aggregate and a 404 from an unmapped URL are the same shape, so a client needs one
/// error path, not two.
/// </para>
/// </summary>
public static class ProblemDetailsCustomizer
{
    public static void Customize(ProblemDetailsContext context)
    {
        var problemDetails = context.ProblemDetails;
        var status = problemDetails.Status ?? context.HttpContext.Response.StatusCode;

        // The exception handler has already chosen a catalogue entry; anything else (a framework
        // status code, a hand-rolled Results.Problem) gets the entry for its status so that no error
        // escapes without a machine-readable code.
        if (!problemDetails.Extensions.ContainsKey("code"))
        {
            var problem = ApiProblems.FromStatus(status);

            problemDetails.Extensions["code"] = problem.Code;
            problemDetails.Status ??= problem.Status;
            problemDetails.Type ??= problem.Type;
            problemDetails.Title ??= problem.Title;
        }

        // RFC 9457 `instance`: which request hit the problem. Path only — a query string can carry
        // tokens or personal data, and this body may be logged or pasted into a support ticket.
        problemDetails.Instance ??= context.HttpContext.Request.Path.Value;

        // The W3C trace id, not HttpContext.TraceIdentifier: it is the id OpenTelemetry exports, so
        // pasting it into the Aspire dashboard finds the exact trace. Fall back only when untraced.
        problemDetails.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

        // Echoes the caller's own X-Correlation-ID, which may span several of our services.
        if (CorrelationIdMiddleware.TryGet(context.HttpContext, out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }
    }
}
