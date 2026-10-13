namespace Kadari.WebApi.Configuration;

/// <summary>
/// The token Meta's webhook verification handshake must present (GL-40). Twilio does not use the handshake; without a
/// token configured the GET answers 404, so the endpoint is not an open echo of caller-supplied text.
/// </summary>
public sealed class WhatsAppWebhookOptions
{
    public const string SectionName = "WhatsApp";

    public string VerifyToken { get; init; } = string.Empty;
}
