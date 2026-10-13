namespace Kadari.Application.Abstractions.Identity;

/// <summary>
/// Delivers an OTP code to a mobile number. Stubbed until the Notification capability (#8) + a
/// provider (Meta WhatsApp / Twilio) exist.
/// </summary>
public interface IOtpDelivery
{
    Task SendAsync(string mobileNumber, string code, CancellationToken cancellationToken = default);
}
