using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

public readonly record struct BrandId(Guid Value);

/// <summary>
/// A product brand in the Care registry (CARE/12 §3), with the South African operator that answers for it and the
/// research behind it. Rules reference a brand by id; customers' free-text brands resolve to one by
/// <see cref="Slug"/>. Reference data, imported through the registry import — never edited ad hoc.
/// </summary>
public sealed class Brand : Entity<BrandId>
{
    public const int MaxNameLength = 120;
    public const int MaxTextLength = 1000;

    private Brand() { }   // EF

    private Brand(BrandId id, string name) : base(id)
    {
        Name = name;
        Slug = RegistrySlug.For(name);
    }

    public string Name { get; private set; } = null!;

    /// <summary>The lookup key: lower-case letters and digits only ("De'Longhi" → "delonghi").</summary>
    public string Slug { get; private set; } = null!;

    public string? ParentGroup { get; private set; }
    public string? LocalOperator { get; private set; }
    public string? Categories { get; private set; }
    public string? ServiceModel { get; private set; }
    public string? RegistrationSignal { get; private set; }
    public string? Priority { get; private set; }
    public RegistryConfidence ResearchConfidence { get; private set; }
    public SourceEvidenceId? SourceEvidenceId { get; private set; }

    public static Brand Create(BrandId id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var clean = name.Trim();
        return clean.Length > MaxNameLength ? throw new DomainException("A brand name is too long.") : new Brand(id, clean);
    }

    /// <summary>Apply the research facts from an import. Returns whether anything changed.</summary>
    public bool Describe(
        string? parentGroup, string? localOperator, string? categories, string? serviceModel,
        string? registrationSignal, string? priority, RegistryConfidence confidence, SourceEvidenceId? source)
    {
        var before = (ParentGroup, LocalOperator, Categories, ServiceModel, RegistrationSignal, Priority, ResearchConfidence, SourceEvidenceId);
        ParentGroup = RegistryText.Clean(parentGroup, MaxTextLength);
        LocalOperator = RegistryText.Clean(localOperator, MaxTextLength);
        Categories = RegistryText.Clean(categories, MaxTextLength);
        ServiceModel = RegistryText.Clean(serviceModel, MaxTextLength);
        RegistrationSignal = RegistryText.Clean(registrationSignal, MaxTextLength);
        Priority = RegistryText.Clean(priority, 8);
        ResearchConfidence = confidence;
        SourceEvidenceId = source;
        return before != (ParentGroup, LocalOperator, Categories, ServiceModel, RegistrationSignal, Priority, ResearchConfidence, SourceEvidenceId);
    }
}
