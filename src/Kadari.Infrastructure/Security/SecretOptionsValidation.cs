using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application.Features.Identity.Services;
using Kadari.Infrastructure.ProductionReadiness;

namespace Kadari.Infrastructure.Security;

/// <summary>
/// Refuses to start a non-Development host whose data-protecting secrets are missing, short, or still the
/// values committed to the repository (GL-02).
/// </summary>
/// <remarks>
/// <para>
/// The failure this replaces was silent. <see cref="FieldProtector"/> falls back to a built-in key when
/// <c>FieldEncryption:Key</c> is unset, and nothing validated <c>IdentityHash:Secret</c> or
/// <c>ReportIntegrity:SigningKey</c> — so a deploy that missed one environment variable encrypted personal data
/// with a key anyone with repository access holds, and came up healthy.
/// </para>
/// <para>
/// Registered by <c>AddInfrastructure</c>, so the worker is covered as well as the API. The worker decrypts and
/// signs the same data; a key it disagrees with the API about is as bad as a missing one.
/// </para>
/// </remarks>
public static class SecretOptionsValidation
{
    /// <summary>Every key here is an HMAC or AES key; 32 bytes is the floor for HMAC-SHA256.</summary>
    public const int MinimumBytes = 32;

    /// <summary>
    /// The secret values committed to <c>appsettings.Development.json</c> and the <see cref="FieldProtector"/>
    /// fallback. Known to anyone with the repository, so never acceptable outside Development.
    /// </summary>
    public static readonly IReadOnlySet<string> DevelopmentValues = new HashSet<string>(StringComparer.Ordinal)
    {
        ProductionReadinessGuard.DevelopmentSigningKey,
        FieldProtector.DevelopmentKey,
        "trustli-local-dev-identity-pepper-change-me!!",
        "trustli-local-dev-field-encryption-key-change-me!!",
        "trustli-local-dev-report-media-key-change-me!!",
        "trustli-local-dev-report-integrity-key-change-me!",
    };

    public static IServiceCollection AddSecretValidation(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        Require<FieldEncryptionOptions>(
            services, configuration, FieldEncryptionOptions.SectionName, o => o.Key, "FieldEncryption:Key", isDevelopment);
        Require<IdentityHashOptions>(
            services, configuration, IdentityHashOptions.SectionName, o => o.Secret, "IdentityHash:Secret", isDevelopment);
        Require<ReportIntegrityOptions>(
            services, configuration, ReportIntegrityOptions.SectionName, o => o.SigningKey, "ReportIntegrity:SigningKey", isDevelopment);
        Require<ReportMediaOptions>(
            services, configuration, ReportMediaOptions.SectionName, o => o.SigningKey, "ReportMedia:SigningKey", isDevelopment);

        return services;
    }

    /// <summary>Development keeps its committed defaults; everywhere else the secret must be real.</summary>
    public static bool IsAcceptable(string? secret, bool isDevelopment)
        => isDevelopment
           || (Encoding.UTF8.GetByteCount(secret ?? string.Empty) >= MinimumBytes && !DevelopmentValues.Contains(secret!));

    private static void Require<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<TOptions, string?> secret,
        string key,
        bool isDevelopment)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(
                o => IsAcceptable(secret(o), isDevelopment),
                $"{key} must be set to a unique secret of at least {MinimumBytes} bytes outside Development "
                + "(the committed development value is refused).")
            .ValidateOnStart();
    }
}
