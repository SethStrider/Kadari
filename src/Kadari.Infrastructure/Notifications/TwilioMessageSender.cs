using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;


namespace Kadari.Infrastructure.Notifications;

/// <summary>
/// One call to Twilio's Messages API (form-encoded, basic auth). Shared by the notification channel and OTP
/// delivery so both authenticate, address and fail the same way.
/// </summary>
/// <remarks>
/// A failed send throws <see cref="HttpRequestException"/> carrying the status code only — Twilio's error body can
/// echo the message, and an OTP must never reach a log (PAS-002 §3).
/// </remarks>
public sealed class TwilioMessageSender(IHttpClientFactory httpClientFactory, IOptions<TwilioOptions> options)
{
    public const string HttpClientName = "twilio";

    public const string WhatsAppPrefix = "whatsapp:";

    private readonly TwilioOptions _options = options.Value;

    public async Task SendAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.ApiBaseUrl.TrimEnd('/')}/2010-04-01/Accounts/{_options.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(fields),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.AccountSid}:{_options.AuthToken}")));

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Twilio rejected the message ({(int)response.StatusCode}).", inner: null, response.StatusCode);
        }
    }
}
