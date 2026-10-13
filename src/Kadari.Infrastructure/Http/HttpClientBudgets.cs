using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Kadari.Infrastructure.Http;

/// <summary>
/// Per-client time budgets over the standard resilience pipeline (GL-11). ServiceDefaults gives every HttpClient a
/// 10 s attempt / 30 s total budget, which cuts a vision model off mid-answer and fails slow registry lookups.
/// </summary>
public static class HttpClientBudgets
{
    /// <summary>
    /// Registers <paramref name="name"/> with its own standard pipeline and an attempt budget of
    /// <paramref name="attemptTimeout"/>.
    /// </summary>
    /// <remarks>
    /// The defaults' handler is shared by all clients, so it cannot be tuned per name; this client replaces it with
    /// its own. Unsafe methods stay unretried, as in the defaults. The circuit breaker's sampling window must be at
    /// least twice the attempt timeout, or the pipeline refuses to build.
    /// </remarks>
    public static IHttpClientBuilder AddHttpClientWithBudget(
        this IServiceCollection services, string name, TimeSpan attemptTimeout)
    {
        var builder = services.AddHttpClient(name);
        // Experimental API, but the documented way to override ConfigureHttpClientDefaults for one client. The
        // behaviour is pinned by OutboundResilienceTests, so a breaking change fails the build's tests, not prod.
#pragma warning disable EXTEXP0001
        builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.DisableForUnsafeHttpMethods();
            options.AttemptTimeout.Timeout = attemptTimeout;
            options.TotalRequestTimeout.Timeout = attemptTimeout + TimeSpan.FromSeconds(15);
            options.CircuitBreaker.SamplingDuration = attemptTimeout * 2;
        });

        return builder;
    }
}
