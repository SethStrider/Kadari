using Kadari.Domain.Abstractions;
using Kadari.Domain.ValueObjects;

namespace Kadari.Domain.Assets;

public readonly record struct AssetEvidenceId(Guid Value)
{
    public static AssetEvidenceId New() => new(Guid.CreateVersion7());
}

/// <summary>What an evidence item asserts about an asset. Each kind contributes to a different rung of the
/// assurance ladder (see <c>AssetAssurance</c>): an image is an observation, an identification names the
/// item, ownership evidence supports that it is the subject's, value evidence supports what it is worth.
/// <see cref="Purchase"/> carries the structured purchase facts KADARI Care reads (brand, model, retailer, date);
/// it is not a rung of the ladder. <see cref="Registration"/> is a provider's confirmation that the product was
/// registered (its reference); not a rung either.</summary>
public enum AssetEvidenceKind
{
    Image,
    Identification,
    Ownership,
    Value,
    Purchase,
    Registration,
}

/// <summary>
/// How independent an assertion is (deck §16 — "evidence strength increases as independent support is
/// added"). <see cref="SelfAsserted"/> is machine/owner-derived (a device image, a Gemini identification,
/// an AI value estimate); <see cref="Independent"/> is corroborated from outside (a receipt, a serial, a
/// professional valuation). Only independent support lifts ownership/value up the ladder (D4/D10).
/// </summary>
public enum EvidenceStrength
{
    SelfAsserted,
    Independent,
}

/// <summary>
/// A single provenance-carrying assertion about an <see cref="Asset"/> (D4). A child of the Asset
/// aggregate — the aggregate is the consistency boundary because its assurance is derived from the whole
/// evidence set. Append-only (audit): an assertion is never mutated; a newer one is added instead.
/// <see cref="Confidence"/> is the recognition axis and is kept *separate* from assurance — a 97%
/// identification is still just an identification.
/// </summary>
public sealed class AssetEvidence : Entity<AssetEvidenceId>
{
    private AssetEvidence() { }   // EF

    private AssetEvidence(
        AssetEvidenceId id, AssetEvidenceKind kind, EvidenceStrength strength, string source,
        DateTimeOffset assertedAt, double? confidence, string? reference, Money? amount,
        DateTimeOffset? retentionUntil = null) : base(id)
    {
        Kind = kind;
        Strength = strength;
        Source = source;
        AssertedAt = assertedAt;
        Confidence = confidence;
        Reference = reference;
        Amount = amount;
        RetentionUntil = retentionUntil;
    }

    public AssetEvidenceKind Kind { get; private set; }
    public EvidenceStrength Strength { get; private set; }

    /// <summary>Who/what produced the assertion (e.g. "device", "gemini", "receipt", "valuer").</summary>
    public string Source { get; private set; } = null!;

    public DateTimeOffset AssertedAt { get; private set; }

    /// <summary>Recognition confidence in [0,1] where the source reports one — a display axis, not assurance.</summary>
    public double? Confidence { get; private set; }

    /// <summary>An opaque link to the underlying artefact (blob key, external id, note), where relevant.</summary>
    public string? Reference { get; private set; }

    /// <summary>The asserted value — only on <see cref="AssetEvidenceKind.Value"/> evidence; null otherwise.</summary>
    public Money? Amount { get; private set; }

    /// <summary>The purchase facts — only on <see cref="AssetEvidenceKind.Purchase"/> evidence; null otherwise.</summary>
    public PurchaseFacts? Purchase { get; private set; }

    /// <summary>
    /// The retention horizon for a two-tier policy (D8/POPIA). <c>null</c> = the <b>canonical</b> tier: a
    /// piece of evidence kept long-term on the insurance lawful basis (claims readiness). A value = the
    /// <b>ephemeral</b> tier: a throwaway scan frame the retention sweep purges once <see cref="RetentionUntil"/>
    /// passes, distinct from the KYC evidence horizon.
    /// </summary>
    public DateTimeOffset? RetentionUntil { get; private set; }

    /// <summary>When the ephemeral evidence was purged by the retention sweep — null while it is still held.</summary>
    public DateTimeOffset? PurgedAt { get; private set; }

    /// <summary>Whether this is an ephemeral, retention-bounded item (rather than canonical, kept evidence).</summary>
    public bool IsEphemeral => RetentionUntil is not null;

    /// <summary>Whether this item is past its retention window and not yet purged, at <paramref name="now"/>.</summary>
    public bool IsExpired(DateTimeOffset now) => RetentionUntil is { } until && until < now && PurgedAt is null;

    /// <summary>An observed image of the asset (a captured still) — the canonical evidence image, retained
    /// long-term on the insurance basis (D8). Self-asserted.</summary>
    public static AssetEvidence Observed(string source, string imageReference, DateTimeOffset assertedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageReference);
        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Image, EvidenceStrength.SelfAsserted,
            source.Trim(), assertedAt, confidence: null, reference: imageReference.Trim(), amount: null);
    }

    /// <summary>An ephemeral scan frame captured during analysis — the throwaway tier (D8): kept only until
    /// <paramref name="retentionUntil"/>, then purged by the retention sweep. Self-asserted image evidence.</summary>
    public static AssetEvidence EphemeralFrame(
        string source, string imageReference, DateTimeOffset assertedAt, DateTimeOffset retentionUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageReference);
        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Image, EvidenceStrength.SelfAsserted,
            source.Trim(), assertedAt, confidence: null, reference: imageReference.Trim(), amount: null, retentionUntil);
    }

    /// <summary>
    /// Purge this ephemeral item (POPIA data minimisation): mark it purged and forget its blob reference,
    /// which the caller deletes from storage first. Idempotent and only for ephemeral items — canonical
    /// evidence is retained. Returns the reference to delete, or null when there is nothing to purge.
    /// </summary>
    internal string? Purge(DateTimeOffset now)
    {
        if (!IsEphemeral || PurgedAt is not null)
        {
            return null;
        }

        var reference = Reference;
        Reference = null;
        PurgedAt = now;
        return reference;
    }

    /// <summary>
    /// Forget this item's file on the owner's POPIA erasure (AS-03): clear the reference — a photo, receipt or
    /// certificate key, or a free-text note — and return it so the caller can delete the file. Unlike
    /// <see cref="Purge"/> this applies to canonical evidence too: erasure overrides the insurance-basis
    /// retention. The facts (kind, strength, amount) stay; they no longer identify anyone. Idempotent.
    /// </summary>
    internal string? EraseFile(DateTimeOffset now)
    {
        if (Reference is null)
        {
            return null;
        }

        var reference = Reference;
        Reference = null;
        PurgedAt ??= now;
        return reference;
    }

    /// <summary>An identification of the item (make/model/category), typically from the vision enricher. Self-asserted.</summary>
    public static AssetEvidence Identified(string source, double confidence, DateTimeOffset assertedAt, string? reference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (confidence is < 0 or > 1)
        {
            throw new DomainException("Confidence is a fraction in [0, 1].");
        }

        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Identification, EvidenceStrength.SelfAsserted,
            source.Trim(), assertedAt, confidence, string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(), amount: null);
    }

    /// <summary>Independent support that the item belongs to the subject (a receipt, a serial match).</summary>
    public static AssetEvidence OwnershipSupported(string source, string reference, DateTimeOffset assertedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Ownership, EvidenceStrength.Independent,
            source.Trim(), assertedAt, confidence: null, reference: reference.Trim(), amount: null);
    }

    /// <summary>An AI/self-asserted baseline replacement value (D10) — a displayed number that does NOT reach
    /// ValueSupported on its own.</summary>
    public static AssetEvidence ValueEstimated(string source, Money amount, DateTimeOffset assertedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(amount);
        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Value, EvidenceStrength.SelfAsserted,
            source.Trim(), assertedAt, confidence: null, reference: null, amount);
    }

    /// <summary>An independently-supported value (a receipt or a professional valuation) — this is what lifts
    /// the asset to ValueSupported (D10).</summary>
    public static AssetEvidence ValueSupported(string source, Money amount, string reference, DateTimeOffset assertedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Value, EvidenceStrength.Independent,
            source.Trim(), assertedAt, confidence: null, reference: reference.Trim(), amount);
    }

    /// <summary>
    /// The purchase facts the owner confirmed (KADARI Care): brand, model, retailer and purchase date. Independent when
    /// a stored receipt backs them (<paramref name="receiptReference"/>), self-asserted otherwise — the coverage engine
    /// only calls a warranty <i>active</i> on independent purchase evidence. A correction is a new item; the latest
    /// wins, so nothing confirmed earlier is overwritten.
    /// </summary>
    public static AssetEvidence PurchaseRecorded(
        string source, PurchaseFacts facts, DateTimeOffset assertedAt, string? receiptReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(facts);
        var strength = string.IsNullOrWhiteSpace(receiptReference) ? EvidenceStrength.SelfAsserted : EvidenceStrength.Independent;
        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Purchase, strength, source.Trim(), assertedAt,
            confidence: null, reference: string.IsNullOrWhiteSpace(receiptReference) ? null : receiptReference.Trim(), amount: null)
        {
            Purchase = facts,
        };
    }

    /// <summary>The provider's confirmation reference for a warranty registration (KADARI Care). Self-asserted: the owner
    /// typed what the provider showed them.</summary>
    public static AssetEvidence RegistrationConfirmed(string provider, string reference, DateTimeOffset assertedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        var source = provider.Trim();
        return new AssetEvidence(AssetEvidenceId.New(), AssetEvidenceKind.Registration, EvidenceStrength.SelfAsserted,
            source.Length > 80 ? source[..80] : source, assertedAt, confidence: null, reference: reference.Trim(), amount: null);
    }

    /// <summary>A professional (independent) valuation submitted by a valuer (D10) — an <see cref="ValueSupported"/>
    /// value whose <see cref="Source"/> is the valuer and whose <see cref="Reference"/> is their certificate. The
    /// human-in-the-loop rung the AI baseline cannot reach; it is what upgrades jewellery/art to ValueSupported.</summary>
    public static AssetEvidence ProfessionallyValued(string valuer, Money amount, string certificateReference, DateTimeOffset assertedAt)
        => ValueSupported(valuer, amount, certificateReference, assertedAt);
}
