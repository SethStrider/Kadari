using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Domain.Notifications;

namespace Kadari.Infrastructure.Notifications;

/// <summary>
/// Routes OTP delivery through the notification capability (PCS-005 §1: no direct sends), replacing
/// the old standalone stub delivery. OTP is pre-auth (no customer yet), so it sends via the channel
/// directly rather than the template pipeline.
/// </summary>
public sealed class NotificationOtpDelivery(INotificationChannel channel) : IOtpDelivery
{
    public Task SendAsync(string mobileNumber, string code, CancellationToken cancellationToken = default)
        => channel.SendAsync(
            NotificationChannel.WhatsApp, mobileNumber,
            $"Your Trustli verification code is {code}. It expires in 5 minutes.",
            containsSecret: true,   // the body is the credential — never log it (PAS-002 §3)
            cancellationToken);
}
