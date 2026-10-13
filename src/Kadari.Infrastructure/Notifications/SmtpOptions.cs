namespace Kadari.Infrastructure.Notifications;

/// <summary>How the SMTP connection is secured. There is deliberately no plaintext option (GL-10).</summary>
public enum SmtpSecurity
{
    /// <summary>Submission port 587: connect, then upgrade with STARTTLS — refused if the server cannot.</summary>
    StartTls,

    /// <summary>Port 465: TLS from the first byte.</summary>
    ImplicitTls,
}

/// <summary>
/// The transactional email provider's SMTP submission settings (GL-10). Unset <see cref="Host"/> means no email is
/// sent; <c>NotificationChannel.Email</c> then fails loudly instead of disappearing.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public SmtpSecurity Security { get; init; } = SmtpSecurity.StartTls;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>A DKIM-signed sending address, e.g. <c>no-reply@mail.trustli.co.za</c>.</summary>
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Trustli";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
