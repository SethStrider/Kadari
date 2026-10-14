using Kadari.Domain.Abstractions;
using Kadari.Domain.Customers;

namespace Kadari.Domain.Owners;

public readonly record struct OwnerId(Guid Value)
{
    public static OwnerId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// A subject of assessment (DES-001b) — the person or business a decision is about, made first-class so one
/// subject can span many decisions/journeys (enterprise, one-to-many). It formalises what was implicit
/// before (a <see cref="Customer"/> for self-verification, an OSINT entity for a footprint) without
/// disturbing those aggregates: a person subject is typically <b>backed by</b> a Customer (the onboarded
/// account); a business or third-party subject has no customer link (<see cref="CustomerId"/> is null),
/// which is what enables counterparty vetting later.
/// </summary>
public sealed class Owner : AggregateRoot<OwnerId>
{
    private Owner() { }   // EF

    private Owner(
        OwnerId id, OwnerType type, string displayName, CustomerId? customerId,
        IReadOnlyDictionary<string, string> identifiers) : base(id)
    {
        Type = type;
        DisplayName = displayName;
        CustomerId = customerId;
        Identifiers = new Dictionary<string, string>(identifiers);
    }

    public OwnerType Type { get; private set; }
    public string DisplayName { get; private set; } = null!;

    /// <summary>The onboarded customer account backing this subject, when it is one of our customers; else null.</summary>
    public CustomerId? CustomerId { get; private set; }

    /// <summary>Known identifiers (ID number, company registration, domain…), keyed by a stable name.</summary>
    public IReadOnlyDictionary<string, string> Identifiers { get; private set; } = new Dictionary<string, string>();

    /// <summary>A non-reversible key (keyed HMAC) derived from this subject's verified national ID, stamped
    /// once an identity check verifies. It outlives the POPIA purge of the raw evidence and lets a passport
    /// claim prove the claimant is this subject by matching keys — the subject never stores the ID itself.
    /// Null until an identity rail has verified the subject.</summary>
    public string? IdentityKeyHash { get; private set; }

    /// <summary>A person subject backed by an onboarded customer.</summary>
    public static Owner ForCustomer(CustomerId customerId, string displayName, IReadOnlyDictionary<string, string>? identifiers = null)
        => Create(OwnerType.Person, displayName, customerId, identifiers);

    /// <summary>A person subject not (yet) linked to a customer account — e.g. a third party being assessed.</summary>
    public static Owner ForPerson(string displayName, IReadOnlyDictionary<string, string>? identifiers = null)
        => Create(OwnerType.Person, displayName, null, identifiers);

    /// <summary>A business subject.</summary>
    public static Owner ForBusiness(string displayName, IReadOnlyDictionary<string, string>? identifiers = null)
        => Create(OwnerType.Business, displayName, null, identifiers);

    /// <summary>Back this subject with an onboarded customer account (idempotent — the first link wins).</summary>
    public void LinkToCustomer(CustomerId customerId) => CustomerId ??= customerId;

    /// <summary>Stamp the subject's identity key from a verified identity (keyed HMAC of the national ID).
    /// The latest verified identity wins.</summary>
    public void SetIdentityKey(string identityKeyHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityKeyHash);
        IdentityKeyHash = identityKeyHash;
    }

    /// <summary>
    /// POPIA erasure of the person behind this subject (AS-03): tombstone the name and forget the identifiers and
    /// identity key, so nothing that hangs off the subject (assets, evidence, passports) identifies them any
    /// more. The id survives so history stays intact. Idempotent.
    /// </summary>
    public void Anonymize()
    {
        DisplayName = "[erased]";
        Identifiers = new Dictionary<string, string>();
        IdentityKeyHash = null;
    }

    private static Owner Create(
        OwnerType type, string displayName, CustomerId? customerId, IReadOnlyDictionary<string, string>? identifiers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new Owner(OwnerId.New(), type, displayName.Trim(), customerId,
            identifiers ?? new Dictionary<string, string>());
    }
}
