namespace Kadari.WebApi.Middleware;

/// <summary>
/// Ensures every request carries a correlation id (API design standard, #3): honours an inbound
/// <c>X-Correlation-ID</c> or mints one, echoes it on the response, and pushes it into the log scope
/// so a whole request is traceable by a single id.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    private const string ItemKey = "Trustli.CorrelationId";

    /// <summary>An id longer than this is not a correlation id; it is someone probing the log pipeline.</summary>
    private const int MaxLength = 128;

    /// <summary>Reads the correlation id assigned to <paramref name="context"/>, if the middleware has run.</summary>
    public static bool TryGet(HttpContext context, out string correlationId)
    {
        if (context.Items.TryGetValue(ItemKey, out var value) && value is string id)
        {
            correlationId = id;
            return true;
        }

        correlationId = string.Empty;
        return false;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[ItemKey] = correlationId;

        // Registered as an OnStarting callback rather than written now: UseExceptionHandler clears the
        // response before writing a problem body, which would drop a header set here. OnStarting runs
        // immediately before the flush, so the id survives on error responses — exactly the responses
        // a caller most needs to correlate.
        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            if (TryGet(ctx, out var id))
            {
                ctx.Response.Headers[HeaderName] = id;
            }

            return Task.CompletedTask;
        }, context);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Takes the caller's id when it is safe to echo and to write into logs, and mints one otherwise.
    /// An inbound header is attacker-controlled: it lands in log lines and in a response header, so an
    /// unbounded or control-character value is a log-forging and header-splitting vector.
    /// </summary>
    private static string ResolveCorrelationId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var inbound))
        {
            return NewId();
        }

        var candidate = inbound.ToString();

        return string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength || !IsSafe(candidate)
            ? NewId()
            : candidate;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static bool IsSafe(string value)
    {
        foreach (var c in value)
        {
            // Printable ASCII only: excludes CR/LF (header splitting, log forging) and anything a log
            // viewer might render as control input.
            if (c is < ' ' or > '~')
            {
                return false;
            }
        }

        return true;
    }
}
