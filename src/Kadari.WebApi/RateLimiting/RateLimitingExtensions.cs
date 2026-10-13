using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Kadari.WebApi.ExceptionHandling;

namespace Kadari.WebApi.RateLimiting;

/// <summary>
/// Request rate limits (PAS-002 §7). Nothing throttled anything before this: OTP requests could be
/// enumerated across numbers (each one an SMS cost), OTP verification brute-forced, and the public
/// webhooks — which front a metered LLM — driven without bound.
/// </summary>
/// <remarks>
/// <para>
/// <b>Partitioning uses the client IP</b>, which is only trustworthy because <c>UseForwardedHeaders</c>
/// runs first and is now restricted to configured proxies (PAS-002 §13.3). Without that restriction
/// any caller could spoof <c>X-Forwarded-For</c> and mint a fresh partition per request.
/// </para>
/// <para>
/// <b>These limits are per-instance.</b> In-memory partitions do not coordinate across replicas, so the
/// effective limit multiplies by instance count. Acceptable at launch; a Redis-backed partition store
/// is the follow-up before horizontal scale-out.
/// </para>
/// </remarks>
public static class RateLimitingExtensions
{
    public const string AuthOtpRequestPolicy = "auth-otp-request";
    public const string AuthOtpVerifyPolicy = "auth-otp-verify";
    public const string AuthPasswordLoginPolicy = "auth-password-login";
    public const string WebhookPolicy = "webhook";
    public const string AuthRefreshPolicy = "auth-refresh";
    public const string SnapCapturePolicy = "kadari-capture";

    /// <summary>Snaps per user per minute. Each one is a paid vision-model call holding a slot for up to 90 s.</summary>
    public const int SnapCapturePerMinute = 20;

    /// <summary>Refreshes per source per minute — a browser needs one every 15 minutes.</summary>
    public const int AuthRefreshPerMinute = 20;

    public static IServiceCollection AddKadariRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Requesting a code costs us an SMS/WhatsApp send. The handler additionally enforces a
            // 60-second resend window per mobile number; this bounds enumeration *across* numbers,
            // which that check cannot see.
            options.AddPolicy(AuthOtpRequestPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                }));

            // Complements — does not replace — the persisted per-challenge attempt counter, which is
            // the durable control. This one bounds how fast a single source can grind any challenge.
            options.AddPolicy(AuthOtpVerifyPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                }));

            // Password sign-in for the staff portal. Complements — does not replace — the persisted
            // per-account lockout (the durable control); this bounds how fast one source can spray
            // guesses across many staff emails, which the per-account counter cannot see.
            options.AddPolicy(AuthPasswordLoginPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                }));

            // Providers retry aggressively by design; this is a ceiling on abuse, not on Twilio.
            options.AddPolicy(WebhookPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            // Token refresh (GL-25): a legitimate client refreshes once per access-token lifetime; this bounds
            // how fast one source can spray stolen or guessed refresh tokens. Reuse detection is the durable control.
            options.AddPolicy(AuthRefreshPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AuthRefreshPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            // Kadari capture (GL-17): each snap is a paid Gemini call and holds one of a few concurrency slots,
            // so one account (or a stolen token) must not be able to spend 300 a minute. Partitioned by subject.
            options.AddPolicy(SnapCapturePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirst("sub")?.Value is { } subject ? $"sub:{subject}" : $"ip:{ClientKey(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = SnapCapturePerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Global fallback. Authenticated callers are partitioned by subject so one noisy tenant
            // cannot exhaust an IP-shared budget for everyone behind the same NAT.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var subject = context.User.FindFirst("sub")?.Value;

                return subject is not null
                    ? RateLimitPartition.GetFixedWindowLimiter($"sub:{subject}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                    : RateLimitPartition.GetFixedWindowLimiter($"ip:{ClientKey(context)}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });

            // The "every error is ProblemDetails" contract holds for throttling too (api-standards §4).
            // Written here rather than left to UseStatusCodePages because the limiter short-circuits.
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problem = ApiProblems.TooManyRequests;

                context.HttpContext.Response.StatusCode = problem.Status;
                context.HttpContext.Response.ContentType = "application/problem+json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = problem.Status,
                        Type = problem.Type,
                        Title = problem.Title,
                        Instance = context.HttpContext.Request.Path,
                        Extensions =
                        {
                            ["code"] = problem.Code,
                            ["traceId"] = context.HttpContext.TraceIdentifier,
                        },
                    },
                    cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// The partition key for an unauthenticated caller. Falls back to a single shared bucket when the
    /// address is unavailable — sharing a limit is the safe direction; skipping one is not.
    /// </summary>
    private static string ClientKey(HttpContext context) => ClientKey(context.Connection.RemoteIpAddress);

    /// <summary>
    /// The rate-limit bucket for an address (AS-11). An IPv6 customer line is a whole /64, so keying on the full
    /// address would hand one caller 2^64 separate buckets; IPv6 is keyed by its /64 prefix, IPv4 (and IPv4 mapped
    /// into IPv6) by the full address.
    /// </summary>
    public static string ClientKey(System.Net.IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new System.Net.IPAddress(bytes) + "/64";
    }
}
