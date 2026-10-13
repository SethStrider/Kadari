using System.Text.Json;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Identity;


namespace Kadari.Infrastructure.Notifications;

/// <summary>
/// Delivers sign-in codes through Twilio (GL-01). With an approved WhatsApp authentication template configured the
/// code goes over WhatsApp as the template's <c>{{1}}</c>; otherwise it goes by SMS.
/// </summary>
/// <remarks>
/// OTP is the one message sent to people who have never messaged us, so it cannot rely on a WhatsApp session:
/// free-form text would be rejected by Meta. The code only ever travels as a template variable or an SMS body and is
/// never logged (<see cref="TwilioMessageSender"/> keeps error bodies out of exceptions).
/// </remarks>
public sealed class TwilioOtpDelivery(TwilioMessageSender sender, IOptions<TwilioOptions> options) : IOtpDelivery
{
    private readonly TwilioOptions _options = options.Value;

    public Task SendAsync(string mobileNumber, string code, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.OtpContentSid))
        {
            return sender.SendAsync(new Dictionary<string, string>
            {
                ["From"] = $"{TwilioMessageSender.WhatsAppPrefix}{_options.FromNumber}",
                ["To"] = $"{TwilioMessageSender.WhatsAppPrefix}{mobileNumber}",
                ["ContentSid"] = _options.OtpContentSid,
                ["ContentVariables"] = JsonSerializer.Serialize(new Dictionary<string, string> { ["1"] = code }),
            }, cancellationToken);
        }

        return sender.SendAsync(new Dictionary<string, string>
        {
            ["From"] = _options.SmsFrom,
            ["To"] = mobileNumber,
            ["Body"] = $"Your Trustli verification code is {code}. It expires in 5 minutes.",
        }, cancellationToken);
    }
}
