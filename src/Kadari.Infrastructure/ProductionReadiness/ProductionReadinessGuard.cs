using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.ProductionReadiness;

/// <summary>
/// Fails the host at startup when a development-only placeholder or a known-insecure default is bound
/// outside Development (PAS-002 §3).
/// </summary>
/// <remarks>
/// This runs after the container is built and <b>before the first request is served</b>. A
/// misconfigured deploy must crash loudly: the failure mode this replaces was silent, where a missing
/// <c>Twilio:AccountSid</c> bound a stub whose signature check returned <c>true</c> for everything, and
/// the service came up looking healthy while accepting anonymous webhooks.
/// </remarks>
public static class ProductionReadinessGuard
{
    /// <summary>The ports where a placeholder implementation is a security problem, not just a gap.</summary>
    private static readonly Type[] CriticalPorts =
        [typeof(INotificationChannel), typeof(Application.Abstractions.Security.ISensitiveDataProtector)];

    /// <summary>The signing key shipped in appsettings.Development.json — never valid elsewhere.</summary>
    public const string DevelopmentSigningKey = "kadari-local-dev-signing-key-change-me-32b!!!";

    /// <summary>
    /// Throws when the host is not Development and any critical port resolves to an
    /// <see cref="IDevelopmentOnlyStub"/>, or a development secret is still configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">The host is not safe to serve traffic.</exception>
    public static void AssertProductionReady(IServiceProvider services, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        using var scope = services.CreateScope();
        var problems = new List<string>();

        var stubs = CriticalPorts
            .Select(port => scope.ServiceProvider.GetService(port))
            .OfType<IDevelopmentOnlyStub>()
            .Select(stub => stub.GetType().Name)
            .ToArray();

        if (stubs.Length > 0)
        {
            problems.Add(
                $"development-only stubs are bound for critical ports ({string.Join(", ", stubs)}) — " +
                "configure the real providers");
        }

        var jwt = scope.ServiceProvider.GetService<IOptions<JwtOptions>>()?.Value;
        if (string.Equals(jwt?.SigningKey, DevelopmentSigningKey, StringComparison.Ordinal))
        {
            problems.Add("Jwt:SigningKey is still the shared development key — issue a real secret");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to start in '{environment.EnvironmentName}': {string.Join("; ", problems)}.");
        }
    }
}
