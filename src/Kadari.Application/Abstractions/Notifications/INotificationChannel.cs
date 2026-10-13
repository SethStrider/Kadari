using Kadari.Domain.Notifications;

namespace Kadari.Application.Abstractions.Notifications;

/// <summary>
/// Delivers a rendered message over a channel (WhatsApp/Email/…). The single delivery seam — no
/// product sends directly (PCS-005 §1). Stubbed until real providers exist.
/// </summary>
public interface INotificationChannel
{
    /// <param name="containsSecret">
    /// The body carries a credential the recipient alone may see — an OTP code, a signed link
    /// (PAS-002 §3). Implementations must never write such a body to logs or telemetry. Defaults to
    /// <c>false</c>; callers opt in, because a caller that forgets is the safe direction only when the
    /// content is ordinary.
    /// </param>
    Task SendAsync(
        NotificationChannel channel,
        string recipient,
        string body,
        bool containsSecret = false,
        CancellationToken cancellationToken = default);
}
