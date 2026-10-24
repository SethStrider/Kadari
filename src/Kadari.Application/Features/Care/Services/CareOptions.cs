namespace Kadari.Application.Features.Care.Services;

/// <summary>KADARI Care settings, bound from the <c>Care</c> section.</summary>
public sealed class CareOptions
{
    public const string SectionName = "Care";

    /// <summary>
    /// The country whose registry rules apply (ISO-3166 alpha-2). South Africa is the first registry, not a
    /// hard-coded market (CARE/12 §13): every rule carries its own country, and this picks which apply to the
    /// customers this deployment serves.
    /// </summary>
    public string CountryCode { get; set; } = "ZA";
}
