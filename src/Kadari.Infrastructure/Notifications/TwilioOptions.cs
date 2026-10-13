namespace Kadari.Infrastructure.Notifications;

/// <summary>
/// Twilio WhatsApp channel configuration. When <see cref="AccountSid"/> is set the
/// <see cref="TwilioWhatsAppGateway"/> replaces the stub. Secrets come from user-secrets/environment.
/// </summary>
public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;

    /// <summary>The WhatsApp-enabled sender, E.164 (e.g. "+14155238886").</summary>
    public string FromNumber { get; init; } = string.Empty;

    /// <summary>The public webhook URL Twilio posts to — part of the signature base string.</summary>
    public string WebhookUrl { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://api.twilio.com";

    /// <summary>
    /// The SMS sender (an E.164 number or a registered alphanumeric sender ID). Used for OTP when no WhatsApp
    /// authentication template is configured, and for <c>NotificationChannel.Sms</c> (GL-01).
    /// </summary>
    public string SmsFrom { get; init; } = string.Empty;

    /// <summary>
    /// Content SID (HX…) of the approved WhatsApp <b>Authentication</b> template whose variable <c>{{1}}</c> is the
    /// code. Meta rejects free-form business-initiated WhatsApp text outside a 24-hour session, so an OTP over
    /// WhatsApp must use this template (GL-01). Empty means OTPs go by SMS.
    /// </summary>
    public string OtpContentSid { get; init; } = string.Empty;
}
