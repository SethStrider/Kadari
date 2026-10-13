using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Kadari.Infrastructure.Notifications;

/// <summary>Sends one plain-text email. The seam the notification channel uses for <c>NotificationChannel.Email</c>.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// Email over authenticated SMTP with TLS always on (GL-10): STARTTLS on submission (refused, not downgraded, when
/// the server does not offer it) or implicit TLS. Certificates are validated by the platform — no override.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var message = BuildMessage(_options, to, subject, body);
        using var client = new SmtpClient();

        await client.ConnectAsync(_options.Host, _options.Port, SocketOptionsFor(_options.Security), cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    /// <summary>StartTls, not StartTlsWhenAvailable: a server that cannot upgrade must not receive the password.</summary>
    public static SecureSocketOptions SocketOptionsFor(SmtpSecurity security) => security switch
    {
        SmtpSecurity.ImplicitTls => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };

    public static MimeMessage BuildMessage(SmtpOptions options, string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        return message;
    }
}
