using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Assets;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.CreateVersion7());

    /// <summary>
    /// The well-known tenant every consumer-direct (Ambassador) customer belongs to (D11). A fixed sentinel
    /// so scoping code can assign it without a lookup; an insurer deal later re-stamps a cohort onto its own
    /// <see cref="New"/> tenant. Not a UUIDv7 — deliberately a recognisable constant.
    /// </summary>
    public static readonly TenantId Default = new(new Guid("11111111-1111-1111-1111-111111111111"));
}

/// <summary>Whether a tenant is KADARI itself (the default that owns consumer-direct users) or an insurer
/// (a white-label / licence partner, deck §27–28).</summary>
public enum TenantKind
{
    Platform,
    Insurer,
}

public enum TenantStatus
{
    Active,
    Suspended,
}

/// <summary>
/// A data-partitioning + commercial boundary for Kadari (D2/D11): the party that owns a cohort of
/// customers' asset data. Distinct from an <c>Organisation</c> (an attesting business) and a
/// <c>Connector</c> (an Ambassador referral channel) — this is who *sponsors the customer relationship*.
/// Kept intentionally minimal for v1: insurer branding, contract terms and the insurer console are a
/// deferred fast-follow, so this holds only identity, kind and status. Lives in the Kadari context for
/// now; it graduates to a platform tenancy context if a second product needs tenancy.
/// </summary>
public sealed class Tenant : AggregateRoot<TenantId>
{
    private Tenant() { }   // EF

    private Tenant(TenantId id, string name, TenantKind kind) : base(id)
    {
        Name = name;
        Kind = kind;
        Status = TenantStatus.Active;
    }

    public string Name { get; private set; } = null!;
    public TenantKind Kind { get; private set; }
    public TenantStatus Status { get; private set; }

    /// <summary>White-label branding for the insurer console; null falls back to the platform palette.</summary>
    public TenantBranding? Branding { get; private set; }

    /// <summary>The seeded default tenant that owns consumer-direct customers.</summary>
    public static Tenant CreatePlatformDefault() => new(TenantId.Default, "KADARI", TenantKind.Platform);

    /// <summary>Onboard an insurer as a white-label / licence tenant.</summary>
    public static Tenant CreateInsurer(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Tenant(TenantId.New(), name.Trim(), TenantKind.Insurer);
    }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Activate() => Status = TenantStatus.Active;

    /// <summary>Set (or replace) the tenant's white-label branding for the insurer console.</summary>
    public void Rebrand(TenantBranding branding)
    {
        ArgumentNullException.ThrowIfNull(branding);
        Branding = branding;
    }

    /// <summary>Remove custom branding, reverting the console to the platform palette.</summary>
    public void ClearBranding() => Branding = null;
}
