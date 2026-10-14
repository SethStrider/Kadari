using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets.Events;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Domain.Assets;

public readonly record struct AssetPassportId(Guid Value)
{
    public static AssetPassportId New() => new(Guid.CreateVersion7());
}

/// <summary>Lifecycle of an Asset Passport. Mirrors <c>ConfidencePassport</c>: an active passport verifies as
/// genuine; a suspended or revoked one does not, and history is retained via status rather than deletion.</summary>
public enum AssetPassportStatus
{
    Active,
    Suspended,
    Revoked,
}

/// <summary>
/// The Asset Passport™ (deck §16, spec D9) — a per-important-asset credential that summarises an asset's
/// identity, evidence, assurance and best replacement value. Its own aggregate root (one per asset), copying
/// the <c>ConfidencePassport</c> pattern: minted from the owning <see cref="Asset"/>'s evidence and kept in
/// step as evidence accrues. <see cref="VerificationId"/> is the QR / secure-link target anyone can verify
/// without an account, and the anchor for the deferred exit-to-PoziSnap handoff (spec §7) — so the credential
/// is designed <b>externally verifiable / exportable from day one</b>.
///
/// The passport carries a <i>snapshot</i> of the asset's headline facts (name, category, assurance, best
/// value, evidence count) taken at mint/refresh time, so a verifiable payload can be produced without
/// reloading the whole asset. <see cref="OwnerId"/> and <see cref="TenantId"/> are denormalised from the
/// asset for owner-scoped reads/authz and row-scoping.
/// </summary>
public sealed class AssetPassport : AggregateRoot<AssetPassportId>, ITenantScoped
{
    private AssetPassport() { }   // EF

    private AssetPassport(
        AssetPassportId id, AssetId assetId, OwnerId ownerId, TenantId tenantId, string verificationId,
        DateTimeOffset at) : base(id)
    {
        AssetId = assetId;
        OwnerId = ownerId;
        TenantId = tenantId;
        VerificationId = verificationId;
        Status = AssetPassportStatus.Active;
        IssuedAt = at;
        LastUpdatedAt = at;
    }

    /// <summary>The asset this passport credentials — the owning key (one passport per asset).</summary>
    public AssetId AssetId { get; private set; }

    /// <summary>The subject who owns the asset — denormalised for owner-scoped reads/authz.</summary>
    public OwnerId OwnerId { get; private set; }

    /// <summary>The tenant partition the asset belongs to — denormalised for row-scoping (D2/D11).</summary>
    public TenantId TenantId { get; private set; }

    /// <summary>Stable, unique credential id — the QR / secure-link verification and export target.</summary>
    public string VerificationId { get; private set; } = null!;

    public AssetPassportStatus Status { get; private set; }

    // --- snapshot of the asset's headline facts, refreshed as evidence accrues ---

    /// <summary>The item's best current human label (from identification), snapshotted for the credential.</summary>
    public string DisplayName { get; private set; } = null!;

    public AssetCategory Category { get; private set; }

    /// <summary>The asset's assurance level at the last mint/refresh (derived on the asset, stored here as a
    /// point-in-time snapshot so the credential is self-contained).</summary>
    public AssetAssuranceLevel Assurance { get; private set; }

    /// <summary>The asset's best replacement value at the last mint/refresh; null when none is recorded.</summary>
    public Money? ReplacementValue { get; private set; }

    /// <summary>How many pieces of evidence backed the asset at the last mint/refresh.</summary>
    public int EvidenceCount { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset LastUpdatedAt { get; private set; }

    /// <summary>Mint an asset's passport, snapshotting its current identity, assurance and best value.</summary>
    public static AssetPassport Mint(Asset asset, string verificationId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationId);

        var passport = new AssetPassport(
            AssetPassportId.New(), asset.Id, asset.OwnerId, asset.TenantId, verificationId.Trim(), at);
        passport.Snapshot(asset, at);
        passport.RaiseDomainEvent(new AssetPassportMintedDomainEvent(passport.Id, asset.Id, asset.OwnerId));
        return passport;
    }

    /// <summary>Re-snapshot the credential from the asset as its evidence accrues. Only while active — a
    /// suspended or revoked passport is frozen. No-op if the passport is not for this asset.</summary>
    public void Refresh(Asset asset, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (Status != AssetPassportStatus.Active || asset.Id != AssetId)
        {
            return;
        }

        Snapshot(asset, at);
    }

    private void Snapshot(Asset asset, DateTimeOffset at)
    {
        DisplayName = asset.DisplayName;
        Category = asset.Category;
        Assurance = asset.Assurance;
        ReplacementValue = asset.BestReplacementValue;
        EvidenceCount = asset.Evidence.Count;
        LastUpdatedAt = at;
    }

    /// <summary>Re-point the credential to a new owner when the asset changes hands (transfer/inheritance) —
    /// keeps the denormalised <see cref="OwnerId"/> in step so the new owner's scoped reads resolve it.</summary>
    public void ReassignOwner(OwnerId newOwner, DateTimeOffset at)
    {
        OwnerId = newOwner;
        LastUpdatedAt = at;
    }

    public void Suspend(DateTimeOffset at)
    {
        Status = AssetPassportStatus.Suspended;
        LastUpdatedAt = at;
    }

    public void Revoke(DateTimeOffset at)
    {
        Status = AssetPassportStatus.Revoked;
        LastUpdatedAt = at;
    }

    /// <summary>Whether the credential currently verifies as genuine (active).</summary>
    public bool IsValid => Status == AssetPassportStatus.Active;
}
