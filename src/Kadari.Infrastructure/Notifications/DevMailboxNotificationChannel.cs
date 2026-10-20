using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Kadari.Application.Abstractions;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Domain.Notifications;

namespace Kadari.Infrastructure.Notifications;

/// <summary>
/// Development only: delivers every message, SMS and WhatsApp included, to the local Mailpit inbox the AppHost runs
/// (http://localhost:8400), so a demo can sign in and read notifications without Twilio or an email provider. Each
/// message becomes one email whose subject names the channel and the recipient, e.g. "SMS to +27825550101".
/// </summary>
/// <remarks>
/// Unlike <see cref="StubNotificationChannel"/> this shows secret-bearing bodies (sign-in codes) in full: the inbox is
/// a throwaway local container, and reading the code is the point. It is an <see cref="IDevelopmentOnlyStub"/>, so the
/// production readiness guard refuses to start any other environment while it is bound.
/// </remarks>
public sealed class DevMailboxNotificationChannel(DevMailboxEndpoint endpoint) : INotificationChannel, IDevelopmentOnlyStub
{
    private const string FromAddress = "no-reply@kadari.local";

    public async Task SendAsync(
        NotificationChannel channel,
        string recipient,
        string body,
        bool containsSecret = false,
        CancellationToken cancellationToken = default)
    {
        using var message = Build(channel, recipient, body);
        using var client = new SmtpClient();

        // Mailpit listens in plain text with no authentication; this class never runs outside Development.
        await client.ConnectAsync(endpoint.Host, endpoint.Port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    public static MimeMessage Build(NotificationChannel channel, string recipient, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("KADARI (local)", FromAddress));

        if (channel == NotificationChannel.Email)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = FirstLine(body);
        }
        else
        {
            // A phone number is not an address: show it as the display name on a fixed local mailbox.
            message.To.Add(new MailboxAddress(recipient, $"{channel.ToString().ToLowerInvariant()}@kadari.local"));
            message.Subject = $"{channel} to {recipient}: {FirstLine(body)}";
        }

        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    private static string FirstLine(string body)
    {
        var line = body.Split('\n', 2)[0].Trim();
        return line.Length <= 120 ? line : line[..120];
    }
}

/// <summary>Where the local Mailpit accepts SMTP, parsed from Aspire's <c>ConnectionStrings:mailpit</c>.</summary>
public sealed record DevMailboxEndpoint(string Host, int Port)
{
    /// <summary>Accepts Aspire's <c>Endpoint=smtp://host:port</c> form, or a bare <c>smtp://host:port</c>.</summary>
    public static DevMailboxEndpoint? Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var value = connectionString.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase)
            ? connectionString["Endpoint=".Length..]
            : connectionString;

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Port > 0
            ? new DevMailboxEndpoint(uri.Host, uri.Port)
            : null;
    }
}
