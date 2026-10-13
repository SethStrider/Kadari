namespace Kadari.Application.Features.Identity.Services;

/// <summary>
/// Which mobile numbers may receive a sign-in code (AS-10). Every code is a paid SMS or WhatsApp send, so
/// launch accepts South African numbers only; premium international ranges are the usual SMS-pumping target.
/// </summary>
public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    /// <summary>Comma-separated country calling codes, e.g. "27" or "27,264". Empty allows any country.</summary>
    public string AllowedCountryCodes { get; set; } = "27";

    public IReadOnlyList<string> CountryCodes =>
        AllowedCountryCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
