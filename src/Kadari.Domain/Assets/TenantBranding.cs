using System.Text.RegularExpressions;

namespace Kadari.Domain.Assets;

/// <summary>
/// White-label branding for an insurer <see cref="Tenant"/> (deck §27–28): the display colours and logo the
/// Admin console re-skins itself with when one of that tenant's users signs in. Mapped as an EF owned type on
/// the tenant; a tenant with no branding falls back to the platform (Trustli) palette. Colours are ISO hex
/// (<c>#RRGGBB</c>) so the frontend can inject them straight onto its CSS custom properties.
/// </summary>
public sealed partial record TenantBranding
{
    public TenantBranding(string primaryColor, string accentColor, string? logoReference)
    {
        PrimaryColor = NormaliseHex(primaryColor, nameof(primaryColor));
        AccentColor = NormaliseHex(accentColor, nameof(accentColor));

        if (logoReference is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logoReference);
            logoReference = logoReference.Trim();
            if (logoReference.Length > 512)
            {
                throw new ArgumentException("Logo reference is too long (max 512).", nameof(logoReference));
            }
        }

        LogoReference = logoReference;
    }

    /// <summary>Primary brand colour as <c>#RRGGBB</c> — overrides the console's <c>--primary</c> token.</summary>
    public string PrimaryColor { get; }

    /// <summary>Accent/secondary brand colour as <c>#RRGGBB</c> — overrides the console's <c>--accent</c> token.</summary>
    public string AccentColor { get; }

    /// <summary>A reference to the tenant's logo (a blob key or absolute URL); null shows the tenant name only.</summary>
    public string? LogoReference { get; }

    private static string NormaliseHex(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        var trimmed = value.Trim();
        if (!HexColor().IsMatch(trimmed))
        {
            throw new ArgumentException($"'{value}' is not a #RRGGBB hex colour.", paramName);
        }

        return trimmed.ToUpperInvariant();
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
