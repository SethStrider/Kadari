using System.Text.RegularExpressions;

namespace Kadari.Domain.Assets;

/// <summary>
/// One spelling per serial number (AS-14). Owners type <c>sn: ab-123 456</c> or <c>AB123456</c> for the same
/// device; stored as typed, the same serial could sit on several ledgers unnoticed. Kept: letters and digits,
/// upper-cased, after dropping a leading label ("SN:", "S/N", "Serial No."). Everything else (spaces, dashes,
/// slashes) is a separator.
/// </summary>
public static partial class SerialNumbers
{
    /// <summary>The longest serial accepted, after normalising. Real ones are well under this.</summary>
    public const int MaxLength = 64;

    /// <summary>The normalised serial, or null when nothing serial-like is left (fewer than 3 characters).</summary>
    public static string? Normalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var unlabelled = Label().Replace(raw.Trim(), string.Empty);
        var kept = new string(unlabelled.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return kept.Length is >= 3 and <= MaxLength ? kept : null;
    }

    // A label only counts when a separator follows it, so a real serial such as "SN12345" keeps its letters.
    [GeneratedRegex(@"^(serial\s*(number|no\.?)?|s\s*/\s*n|sn)(\s*[:#.]\s*|\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex Label();
}
