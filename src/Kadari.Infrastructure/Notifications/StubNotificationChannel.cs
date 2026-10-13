using Microsoft.Extensions.Logging;
using Kadari.Application.Abstractions;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Domain.Notifications;

namespace Kadari.Infrastructure.Notifications;

/// <summary>
/// TEMPORARY channel: logs the message instead of sending it. Replaced by real WhatsApp (Meta) /
/// Email (SES) providers once credentials exist (PCS-005 §4). Never use in production — the
/// production readiness guard (PAS-002 §3) refuses to start the host while this is bound.
/// </summary>
/// <remarks>
/// Secret-bearing bodies (OTP codes) are redacted even here. A local stub still writes to the same
/// log sinks a developer ships to an aggregator, and an OTP in a log line is an OTP in a search index.
/// The recipient is masked for the same reason — a mobile number is personal information.
/// </remarks>
public sealed class StubNotificationChannel(ILogger<StubNotificationChannel> logger)
    : INotificationChannel, IDevelopmentOnlyStub
{
    public Task SendAsync(
        NotificationChannel channel,
        string recipient,
        string body,
        bool containsSecret = false,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "STUB {Channel} → {Recipient}: {Body}",
            channel,
            Mask(recipient),
            containsSecret ? $"[redacted — {body.Length} chars]" : body);

        return Task.CompletedTask;
    }

    /// <summary>Keeps the last four digits so a developer can still tell which recipient this was.</summary>
    private static string Mask(string recipient)
        => recipient.Length <= 4 ? "****" : $"{new string('*', recipient.Length - 4)}{recipient[^4..]}";
}
