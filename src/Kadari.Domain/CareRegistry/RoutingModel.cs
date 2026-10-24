using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

public readonly record struct RoutingModelId(Guid Value);

/// <summary>
/// One of the route archetypes the routing engine chooses between (the registry's R1–R10: retailer return, OEM
/// direct, authorised service, onsite, carry-in, collection, …). Routing rules reference an archetype by
/// <see cref="Code"/>; the customer never sees it. Reference data, imported.
/// </summary>
public sealed class RoutingModel : Entity<RoutingModelId>
{
    public const int MaxTextLength = 1000;

    private RoutingModel() { }   // EF

    private RoutingModel(RoutingModelId id, string code, string name) : base(id)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? WhenItApplies { get; private set; }
    public string? DefaultAction { get; private set; }
    public string? TypicalEvidence { get; private set; }
    public string? Examples { get; private set; }

    public static RoutingModel Create(RoutingModelId id, string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var cleanCode = code.Trim().ToUpperInvariant();
        return cleanCode.Length > 8
            ? throw new DomainException("A routing model code is at most 8 characters.")
            : new RoutingModel(id, cleanCode, RegistryText.Clean(name, 200)!);
    }

    /// <summary>Apply the research facts from an import. Returns whether anything changed.</summary>
    public bool Describe(string name, string? whenItApplies, string? defaultAction, string? typicalEvidence, string? examples)
    {
        var before = (Name, WhenItApplies, DefaultAction, TypicalEvidence, Examples);
        Name = RegistryText.Clean(name, 200) ?? Name;
        WhenItApplies = RegistryText.Clean(whenItApplies, MaxTextLength);
        DefaultAction = RegistryText.Clean(defaultAction, MaxTextLength);
        TypicalEvidence = RegistryText.Clean(typicalEvidence, MaxTextLength);
        Examples = RegistryText.Clean(examples, MaxTextLength);
        return before != (Name, WhenItApplies, DefaultAction, TypicalEvidence, Examples);
    }
}
