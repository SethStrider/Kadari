using System.Text;
using Kadari.Infrastructure.ProductionReadiness;
using Kadari.Infrastructure.Security;
using Kadari.Infrastructure.Storage;
using Kadari.Infrastructure.Notifications;

namespace Kadari.WebApi.Configuration;

/// <summary>
/// Binds and validates every configuration section the API depends on, failing the host at startup
/// when one is missing or nonsensical (PAS-002 §11).
/// </summary>
/// <remarks>
/// Previously nothing called <c>ValidateOnStart</c>: <c>Jwt:SigningKey</c> defaulted to an empty string
/// and only <c>appsettings.Development.json</c> supplied one, so a production deploy missing the key
/// failed obscurely deep inside token validation rather than at boot. Fail at boot, name the section.
/// </remarks>
public static class OptionsValidationExtensions
{
    public static IHostApplicationBuilder AddValidatedOptions(this IHostApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Jwt:SigningKey is required and must be at least 32 bytes for HS256.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(
                o => o.AccessTokenMinutes is > 0 and <= 60,
                "Jwt:AccessTokenMinutes must be between 1 and 60 — long-lived access tokens outlive revocation.")
            .Validate(
                o => isDevelopment || !string.Equals(
                    o.SigningKey, ProductionReadinessGuard.DevelopmentSigningKey, StringComparison.Ordinal),
                "Jwt:SigningKey is still the shared development key.")
            .ValidateOnStart();

        builder.Services.AddOptions<Access.RefreshCookieOptions>()
            .Bind(builder.Configuration.GetSection(Access.RefreshCookieOptions.SectionName));

        builder.Services.AddOptions<ClientCorsOptions>()
            .Bind(builder.Configuration.GetSection(ClientCorsOptions.SectionName))
            .Validate(
                o => isDevelopment || o.AllowedOrigins.Length > 0,
                "Cors:AllowedOrigins must list at least one origin outside Development — an empty list "
                + "silently ships an API no browser client can call.")
            .Validate(
                o => o.AllowedOrigins.All(origin =>
                    Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && uri.AbsolutePath == "/"
                    && !origin.EndsWith('/')),
                "Cors:AllowedOrigins entries must be absolute origins with no path or trailing slash "
                + "(e.g. \"https://app.kadari.co.za\").")
            .ValidateOnStart();

        builder.Services.AddOptions<ReverseProxyOptions>()
            .Bind(builder.Configuration.GetSection(ReverseProxyOptions.SectionName))
            .Validate(
                o => isDevelopment || !o.TrustsAnyHop,
                "ReverseProxy:KnownProxies or :KnownNetworks must be set outside Development — "
                + "trusting X-Forwarded-For from any hop lets callers spoof the IP that rate limits "
                + "partition on.")
            .Validate(o => o.ForwardLimit > 0, "ReverseProxy:ForwardLimit must be at least 1.")
            .ValidateOnStart();

        builder.Services.AddOptions<ReportMediaOptions>()
            .Bind(builder.Configuration.GetSection(ReportMediaOptions.SectionName))
            .Validate(
                o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "ReportMedia:SigningKey is required and must be at least 32 bytes for HMAC-SHA256.")
            .Validate(
                o => o.LinkLifetimeMinutes is > 0 and <= 1440,
                "ReportMedia:LinkLifetimeMinutes must be between 1 and 1440.")
            .ValidateOnStart();

        builder.Services.AddOptions<S3Options>()
            .Bind(builder.Configuration.GetSection(S3Options.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.BucketName),
                "S3:BucketName is required.")
            .ValidateOnStart();

        // Twilio is opt-in: absent configuration is valid (the logging stub serves in Development). KADARI only sends
        // sign-in codes, so a filled section needs a token and a way to deliver: an SMS number, or a WhatsApp
        // authentication template plus the WhatsApp sender.
        builder.Services.AddOptions<TwilioOptions>()
            .Bind(builder.Configuration.GetSection(TwilioOptions.SectionName))
            .Validate(
                o => string.IsNullOrWhiteSpace(o.AccountSid) || !string.IsNullOrWhiteSpace(o.AuthToken),
                "Twilio:AccountSid is set, so Twilio:AuthToken is required.")
            .Validate(
                o => string.IsNullOrWhiteSpace(o.AccountSid)
                     || !string.IsNullOrWhiteSpace(o.SmsFrom)
                     || (!string.IsNullOrWhiteSpace(o.OtpContentSid) && !string.IsNullOrWhiteSpace(o.FromNumber)),
                "Twilio:AccountSid is set, so Twilio:SmsFrom, or Twilio:OtpContentSid with Twilio:FromNumber, is "
                + "required — otherwise no sign-in code can be delivered.")
            .ValidateOnStart();

        return builder;
    }
}
