using System.Text;
using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

/// <summary>The lookup key for registry names: lower-case ASCII letters and digits only, accents folded.</summary>
public static class RegistrySlug
{
    public static string For(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var folded = name.Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(char.ToLowerInvariant(c));
            }
        }

        return slug.Length == 0 ? throw new DomainException($"'{name}' has no letters or digits to key on.") : slug.ToString();
    }
}

internal static class RegistryText
{
    public static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    public static string Country(string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        var code = countryCode.Trim().ToUpperInvariant();
        return code.Length == 2 && code.All(char.IsAsciiLetterUpper)
            ? code
            : throw new DomainException($"'{countryCode}' is not an ISO-3166 alpha-2 country code.");
    }
}
