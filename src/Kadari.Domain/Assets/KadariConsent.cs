using Kadari.Domain.Abstractions;
using Kadari.Domain.Customers;

namespace Kadari.Domain.Assets;

public readonly record struct KadariConsentId(Guid Value)
{
    public static KadariConsentId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// A distinct thing a customer consents to for Kadari (spec §6, D8/POPIA). Kept separate so each carries
/// its own disclosure and can be withdrawn independently.
/// </summary>
public enum KadariConsentScope
{
    /// <summary>Processing images of the customer's home and belongings to build their asset ledger — the base
    /// lawful basis for the product (retained on an insurance basis).</summary>
    AssetImageProcessing,

    /// <summary>The <b>distinct</b> disclosure that a captured still may be sent to a third-party AI outside SA
    /// (Gemini / a hosted model) for identification — a cross-border transfer the customer opts into separately.</summary>
    CrossBorderAiEnrichment,
}

public enum KadariConsentStatus
{
    Granted,
    Withdrawn,
}

/// <summary>
/// A customer-level Kadari consent (spec §6). Unlike the journey-bound <c>Consent</c>, this is not tied to
/// a verification journey — it records that a customer has granted (or withdrawn) a specific Kadari
/// disclosure. One row per (customer, scope): re-granting a withdrawn consent flips it back rather than
/// creating a duplicate. The cross-border-AI grant is what the capture pipeline checks before sending a home
/// image to an off-shore model.
/// </summary>
public sealed class KadariConsent : AggregateRoot<KadariConsentId>
{
    private KadariConsent() { }   // EF

    private KadariConsent(
        KadariConsentId id, CustomerId customerId, KadariConsentScope scope, DateTimeOffset at) : base(id)
    {
        CustomerId = customerId;
        Scope = scope;
        Status = KadariConsentStatus.Granted;
        GrantedAt = at;
    }

    public CustomerId CustomerId { get; private set; }
    public KadariConsentScope Scope { get; private set; }
    public KadariConsentStatus Status { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }

    /// <summary>Whether the customer currently permits this scope.</summary>
    public bool IsActive => Status == KadariConsentStatus.Granted;

    public static KadariConsent Grant(CustomerId customerId, KadariConsentScope scope, DateTimeOffset at) =>
        new(KadariConsentId.New(), customerId, scope, at);

    /// <summary>Re-grant a previously withdrawn consent (idempotent when already granted).</summary>
    public void Regrant(DateTimeOffset at)
    {
        Status = KadariConsentStatus.Granted;
        GrantedAt = at;
        WithdrawnAt = null;
    }

    public void Withdraw(DateTimeOffset at)
    {
        Status = KadariConsentStatus.Withdrawn;
        WithdrawnAt = at;
    }
}
