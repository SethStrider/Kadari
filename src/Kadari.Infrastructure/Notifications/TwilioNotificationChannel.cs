using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Domain.Notifications;


namespace Kadari.Infrastructure.Notifications;

/// <summary>
/// The production notification channel over Twilio (GL-01): WhatsApp from the WhatsApp sender, SMS from
/// <see cref="TwilioOptions.SmsFrom"/>. Replaces <see cref="StubNotificationChannel"/> whenever Twilio is configured,
/// which is what lets the production readiness guard pass.
/// </summary>
/// <remarks>
/// WhatsApp here is free-form text, which Meta delivers only inside a customer's 24-hour session; outside it,
/// Twilio fails the send. OTP does not come through here for that reason — <see cref="TwilioOtpDelivery"/> uses the
/// approved authentication template. Email goes through <see cref="IEmailSender"/> (SMTP, GL-10) when one is
/// configured, and is refused loudly rather than dropped when not.
/// </remarks>
public sealed class TwilioNotificationChannel(
    TwilioMessageSender sender, IOptions<TwilioOptions> options, IEmailSender? email = null)
    : INotificationChannel
{
    /// <summary>The channel carries no subject; every KADARI email goes out under this one.</summary>
    public const string EmailSubject = "A message from KADARI";

    private readonly TwilioOptions _options = options.Value;

    public Task SendAsync(
        NotificationChannel channel,
        string recipient,
        string body,
        bool containsSecret = false,
        CancellationToken cancellationToken = default)
        => channel switch
        {
            NotificationChannel.WhatsApp => sender.SendAsync(new Dictionary<string, string>
            {
                ["From"] = $"{TwilioMessageSender.WhatsAppPrefix}{_options.FromNumber}",
                ["To"] = $"{TwilioMessageSender.WhatsAppPrefix}{recipient}",
                ["Body"] = body,
            }, cancellationToken),

            NotificationChannel.Sms when !string.IsNullOrWhiteSpace(_options.SmsFrom) => sender.SendAsync(
                new Dictionary<string, string> { ["From"] = _options.SmsFrom, ["To"] = recipient, ["Body"] = body },
                cancellationToken),

            NotificationChannel.Email when email is not null =>
                email.SendAsync(recipient, EmailSubject, body, cancellationToken),

            _ => throw new NotSupportedException($"No {channel} sender is configured for notifications."),
        };
}
