namespace Kadari.Domain.Identity;

/// <summary>
/// One spelling per mobile number (AS-10). People type <c>082 123 4567</c>, <c>27821234567</c>,
/// <c>+27 82 123 4567</c> or <c>0027821234567</c> for the same phone; left as typed, each is a different number
/// with its own OTP budget and its own account. Everything becomes E.164 (<c>+27821234567</c>).
/// </summary>
public static class MobileNumbers
{
    /// <summary>The E.164 form of <paramref name="raw"/>, or null when it is not a phone number. A leading 0 or a
    /// bare 27 is read as South African; other countries need their + (or 00) prefix.</summary>
    public static string? Normalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var plus = raw.TrimStart().StartsWith('+');
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (raw.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')' or '.' or '+')))
        {
            return null;
        }

        string e164;
        if (plus)
        {
            e164 = "+" + digits;
        }
        else if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            e164 = "+" + digits[2..];
        }
        else if (digits.Length == 10 && digits[0] == '0')
        {
            e164 = "+27" + digits[1..];   // a South African local number
        }
        else if (digits.Length == 11 && digits.StartsWith("27", StringComparison.Ordinal))
        {
            e164 = "+" + digits;
        }
        else
        {
            return null;
        }

        // E.164: a country code that does not start with 0, at most 15 digits.
        return e164.Length is >= 8 and <= 16 && e164[1] != '0' ? e164 : null;
    }

    /// <summary>Whether an E.164 number belongs to one of the given country calling codes (e.g. "27").</summary>
    public static bool IsInCountry(string e164, IEnumerable<string> countryCodes) =>
        countryCodes.Any(code => e164.StartsWith("+" + code, StringComparison.Ordinal));
}
