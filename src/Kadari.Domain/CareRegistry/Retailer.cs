using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

public readonly record struct RetailerId(Guid Value);

/// <summary>A retailer or sales channel in the Care registry (CARE/12 §3). Reference data, imported.</summary>
public sealed class Retailer : Entity<RetailerId>
{
    public const int MaxNameLength = 120;
    public const int MaxTextLength = 1000;

    private Retailer() { }   // EF

    private Retailer(RetailerId id, string name, string countryCode) : base(id)
    {
        Name = name;
        Slug = RegistrySlug.For(name);
        CountryCode = countryCode;
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;

    /// <summary>ISO-3166 alpha-2. South Africa is the first registry, not a hard-coded market (CARE/12 §13).</summary>
    public string CountryCode { get; private set; } = null!;

    public string? GroupOwner { get; private set; }
    public string? RetailFormat { get; private set; }
    public string? Categories { get; private set; }
    public string? IntegrationStance { get; private set; }
    public string? Priority { get; private set; }
    public SourceEvidenceId? SourceEvidenceId { get; private set; }

    public static Retailer Create(RetailerId id, string name, string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var clean = name.Trim();
        if (clean.Length > MaxNameLength)
        {
            throw new DomainException("A retailer name is too long.");
        }

        return new Retailer(id, clean, RegistryText.Country(countryCode));
    }

    /// <summary>Apply the research facts from an import. Returns whether anything changed.</summary>
    public bool Describe(
        string? groupOwner, string? retailFormat, string? categories, string? integrationStance, string? priority,
        SourceEvidenceId? source)
    {
        var before = (GroupOwner, RetailFormat, Categories, IntegrationStance, Priority, SourceEvidenceId);
        GroupOwner = RegistryText.Clean(groupOwner, MaxTextLength);
        RetailFormat = RegistryText.Clean(retailFormat, MaxTextLength);
        Categories = RegistryText.Clean(categories, MaxTextLength);
        IntegrationStance = RegistryText.Clean(integrationStance, MaxTextLength);
        Priority = RegistryText.Clean(priority, 8);
        SourceEvidenceId = source;
        return before != (GroupOwner, RetailFormat, Categories, IntegrationStance, Priority, SourceEvidenceId);
    }
}
