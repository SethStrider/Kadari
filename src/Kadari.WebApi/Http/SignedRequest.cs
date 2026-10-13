using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Kadari.WebApi.Configuration;

namespace Kadari.WebApi.Http;

/// <summary>
/// HMAC-SHA256 request signatures for machine-to-machine pushes into Trustli (GL-05).
/// </summary>
/// <remarks>
/// Header: <c>X-Trustli-Signature: t={unix seconds},v1={hex HMAC-SHA256(key, "{t}.{raw body}")}</c>. The timestamp
/// is inside the MAC and must be within the tolerance, so a captured request cannot be replayed later; the body is
/// the raw bytes as sent, so no JSON canonicalisation is needed on either side.
/// </remarks>
public static class SignedRequest
{
    public const string HeaderName = "X-Trustli-Signature";

    public static string Sign(string key, long timestamp, string body)
        => $"t={timestamp.ToString(CultureInfo.InvariantCulture)},v1={Mac(key, timestamp, body)}";

    private static string Mac(string key, long timestamp, string body)
        => Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes($"{timestamp.ToString(CultureInfo.InvariantCulture)}.{body}")));

    public static bool IsValid(string? header, string body, string key, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        long? timestamp = null;
        string? supplied = null;
        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("t=", StringComparison.Ordinal)
                && long.TryParse(part[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var t))
            {
                timestamp = t;
            }
            else if (part.StartsWith("v1=", StringComparison.Ordinal))
            {
                supplied = part[3..];
            }
        }

        if (timestamp is not { } ts || supplied is null
            || Math.Abs(now.ToUnixTimeSeconds() - ts) > (long)tolerance.TotalSeconds)
        {
            return false;
        }

        var expected = Mac(key, ts, body);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied.ToLowerInvariant()));
    }
}

/// <summary>Marks an endpoint whose raw body must stay readable for signature verification.</summary>
public sealed record BufferedBodyMetadata;

/// <summary>
/// Verifies <see cref="SignedRequest"/> on the report-intake push (GL-05). With no key configured the endpoint
/// answers 404 — an unauthenticated write that changes the questions Trustli sends customers must not exist.
/// </summary>
public sealed class ReportIntakeSignatureFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var options = http.RequestServices.GetRequiredService<IOptions<ReportIntakeOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return Results.NotFound();
        }

        // Binding already consumed the body; BufferedBodyMetadata made it rewindable.
        http.Request.Body.Position = 0;
        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(http.RequestAborted);

        var clock = http.RequestServices.GetRequiredService<TimeProvider>();
        var valid = SignedRequest.IsValid(
            http.Request.Headers[SignedRequest.HeaderName], body, options.SigningKey, clock.GetUtcNow(),
            TimeSpan.FromSeconds(options.ToleranceSeconds));

        return valid ? await next(context) : Results.Unauthorized();
    }
}

public static class SignedRequestExtensions
{
    /// <summary>Requires a valid report-intake signature over the raw body.</summary>
    public static TBuilder RequireReportIntakeSignature<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new BufferedBodyMetadata());
        builder.AddEndpointFilter(new ReportIntakeSignatureFilter());
        return builder;
    }

    /// <summary>
    /// Makes the body rewindable for endpoints carrying <see cref="BufferedBodyMetadata"/>, before binding reads it.
    /// </summary>
    public static IApplicationBuilder UseBufferedBodiesForSignedEndpoints(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<BufferedBodyMetadata>() is not null)
            {
                context.Request.EnableBuffering();
            }

            await next(context);
        });
}
