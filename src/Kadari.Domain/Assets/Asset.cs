using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets.Events;
using Kadari.Domain.Subjects;
using Kadari.Domain.ValueObjects;

namespace Kadari.Domain.Assets;

public readonly record struct AssetId(Guid Value)
{
    public static AssetId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// The valuation family an asset belongs to (deck §18). Different families need different replacement
/// intelligence — electronics resolve to an exact/equivalent SKU, jewellery and art need a professional
/// valuation — so this is what a future <c>IValuationRail</c> routes on. Kept deliberately coarse.
/// </summary>
public enum AssetCategory
{
    Electronics,
    Furniture,
    Clothing,
    Jewellery,
    ArtAndCollectibles,
    Other,
}

/// <summary>
/// Where an asset is in its life (deck §30). Only the states v1 acts on are modelled: an asset is
/// <see cref="Active"/> until it is reported stolen, and recovery returns it to active. Ownership
/// transitions (transfer, estate, inheritance — deck §31) are a deferred seam and are intentionally not
/// states here yet; they arrive with the Household/estate work.
/// </summary>
public enum AssetLifecycleStatus
{
    Active,
    ReportedStolen,
}

/// <summary>
/// A physical item in a subject's household (Kadari). Its own aggregate root — the consistency
/// boundary for the item's lifecycle and history, which must outlive any one owner or policy (an asset
/// can be reported stolen and matched by AssetCheck independent of its owner, deck §30). The rich
/// identification and valuation facts live on <c>AssetEvidence</c> (each with its own provenance); the
/// aggregate itself stays lean and holds only what the ledger needs to list it and what governs its
/// lifecycle. Ownership is a reference to a <see cref="SubjectId"/> so a later transfer is a re-point,
/// not a rewrite.
/// </summary>
public sealed class Asset : AggregateRoot<AssetId>, ITenantScoped
{
    private readonly List<AssetEvidence> _evidence = [];
    private readonly List<AssetCoOwner> _coOwners = [];

    private Asset() { }   // EF

    private Asset(
        AssetId id, TenantId tenantId, SubjectId ownerId, string displayName, AssetCategory category,
        string? roomLabel, DateTimeOffset registeredAt) : base(id)
    {
        TenantId = tenantId;
        OwnerId = ownerId;
        DisplayName = displayName;
        Category = category;
        RoomLabel = roomLabel;
        RegisteredAt = registeredAt;
        Status = AssetLifecycleStatus.Active;

        RaiseDomainEvent(new AssetRegisteredDomainEvent(id, ownerId, category));
    }

    /// <summary>The tenant whose data partition this asset belongs to — the row-scoping key (D2/D11).
    /// Consumer-direct assets carry <see cref="TenantId.Default"/>; an insurer cohort carries its own.</summary>
    public TenantId TenantId { get; private set; }

    /// <summary>The append-only evidence supporting this asset. Assurance is derived from this set (D4).</summary>
    public IReadOnlyList<AssetEvidence> Evidence => _evidence.AsReadOnly();

    /// <summary>The most receipts / serials one asset may carry (AS-13).</summary>
    public const int MaxOwnershipProofs = 10;

    /// <summary>The asset's assurance level, derived from its evidence set (never stored — see D4).</summary>
    public AssetAssuranceLevel Assurance => AssetAssurance.For(_evidence);

    /// <summary>
    /// The asset's best replacement-value assertion, or null when none has been recorded (D10). Independently
    /// supported value wins over a self-asserted estimate, and within a strength the most recent wins. Derived
    /// from the evidence set, never stored — the read model and the Asset Passport both source it here so the
    /// rule lives in one place.
    /// </summary>
    public Money? BestReplacementValue =>
        _evidence
            .Where(e => e.Kind == AssetEvidenceKind.Value && e.Amount is not null)
            .OrderByDescending(e => e.Strength == EvidenceStrength.Independent)
            .ThenByDescending(e => e.AssertedAt)
            .Select(e => e.Amount)
            .FirstOrDefault();

    /// <summary>The primary subject who owns the asset — the registrar/capture key, and the seam for transfer.
    /// Household members who share the item are <see cref="CoOwners"/>; the full ownership set is
    /// <see cref="IsOwnedBy"/>.</summary>
    public SubjectId OwnerId { get; private set; }

    /// <summary>The household members who co-own the asset alongside the primary owner (deck §31).</summary>
    public IReadOnlyList<AssetCoOwner> CoOwners => _coOwners.AsReadOnly();

    /// <summary>Whether the subject owns the asset — as the primary owner or a household co-owner. The
    /// authorization/ledger predicate for "is this theirs".</summary>
    public bool IsOwnedBy(SubjectId subject) => OwnerId == subject || _coOwners.Any(c => c.MemberId == subject);

    /// <summary>The best current human label for the item (from identification); denormalised for the ledger.</summary>
    public string DisplayName { get; private set; } = null!;

    public AssetCategory Category { get; private set; }

    /// <summary>The room it was observed in, when the capture flow supplied one. Free text, optional.</summary>
    public string? RoomLabel { get; private set; }

    public AssetLifecycleStatus Status { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>
    /// Register a newly observed item into a subject's ledger. Identification/value facts are attached
    /// separately as evidence; this creates the item and stamps it <see cref="AssetLifecycleStatus.Active"/>.
    /// </summary>
    public static Asset Register(
        TenantId tenantId, SubjectId ownerId, string displayName, AssetCategory category,
        DateTimeOffset registeredAt, string? roomLabel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new Asset(
            AssetId.New(), tenantId, ownerId, displayName.Trim(), category,
            string.IsNullOrWhiteSpace(roomLabel) ? null : roomLabel.Trim(), registeredAt);
    }

    /// <summary>Update the item's human label as identification improves. No-op on an unchanged value.</summary>
    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    /// <summary>Reclassify the item as a better category is established (e.g. after enrichment).</summary>
    public void Reclassify(AssetCategory category) => Category = category;

    /// <summary>Move the asset into a different tenant partition (D2/D11) — used when a customer's cohort is
    /// assigned to an insurer tenant. A re-point of the row-scoping key; no history is lost.</summary>
    public void ReassignTenant(TenantId tenantId) => TenantId = tenantId;

    /// <summary>
    /// Transfer the asset to a new owner — a gift or sale (deck §31). Ownership is a re-point of
    /// <see cref="OwnerId"/>, so the item's identity, evidence and history outlive the change; a
    /// <c>AssetTransferredDomainEvent</c> records it. Only an active item may be transferred (you cannot gift
    /// one reported stolen), and transferring to the current owner is a no-op.
    /// </summary>
    public void Transfer(SubjectId newOwner, DateTimeOffset at)
    {
        if (Status != AssetLifecycleStatus.Active)
        {
            throw new DomainException("Only an active asset can be transferred.");
        }

        if (newOwner == OwnerId)
        {
            return;
        }

        var from = OwnerId;
        OwnerId = newOwner;

        // A transfer hands over the whole asset, so the old household's co-ownership ends — the new owner
        // starts with a clean household. (Inheritance differs: a surviving co-owner keeps their share.)
        _coOwners.Clear();

        RaiseDomainEvent(new AssetTransferredDomainEvent(Id, from, newOwner, at));
    }

    /// <summary>
    /// Pass the asset to an heir on estate settlement (deck §31) — a transfer whose cause is inheritance, so it
    /// applies even to an item reported stolen (the estate passes everything) and raises the distinct
    /// <c>AssetInheritedDomainEvent</c>. Inheriting to the current owner is a no-op.
    /// </summary>
    public void Inherit(SubjectId heir, DateTimeOffset at)
    {
        if (heir == OwnerId)
        {
            return;
        }

        var from = OwnerId;
        OwnerId = heir;
        RaiseDomainEvent(new AssetInheritedDomainEvent(Id, from, heir, at));
    }

    /// <summary>
    /// Attach a piece of evidence (the aggregate owns membership — this is the only way in). Append-only;
    /// evidence is never removed or mutated. Returns the attached item for convenience.
    /// </summary>
    public AssetEvidence Attach(AssetEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        // Proof uploads are bounded (AS-13): each is a stored file, and a real item has a receipt or two, not
        // hundreds.
        if (evidence.Kind == AssetEvidenceKind.Ownership
            && _evidence.Count(e => e.Kind == AssetEvidenceKind.Ownership) >= MaxOwnershipProofs)
        {
            throw new DomainException($"An asset can hold at most {MaxOwnershipProofs} proofs of ownership.");
        }
        _evidence.Add(evidence);
        RaiseDomainEvent(new AssetEvidenceAttachedDomainEvent(Id, evidence.Id, evidence.Kind));
        return evidence;
    }

    /// <summary>
    /// Purge every ephemeral scan frame past its retention window (D8/POPIA), keeping canonical evidence.
    /// The aggregate marks each purged and returns the blob references the caller must delete from storage —
    /// the aggregate does no I/O. Idempotent: an already-purged frame yields nothing.
    /// </summary>
    public IReadOnlyList<string> PurgeExpiredEphemeralEvidence(DateTimeOffset now)
    {
        var deleted = new List<string>();
        foreach (var evidence in _evidence)
        {
            if (!evidence.IsExpired(now))
            {
                continue;
            }

            if (evidence.Purge(now) is { } reference)
            {
                deleted.Add(reference);
            }
        }

        return deleted;
    }

    /// <summary>
    /// Forget every evidence file on this asset when its owner is erased (POPIA, AS-03) and return the
    /// references the caller must delete from storage — the aggregate does no I/O. Idempotent.
    /// </summary>
    public IReadOnlyList<string> EraseEvidenceFiles(DateTimeOffset now) =>
        _evidence.Select(e => e.EraseFile(now)).OfType<string>().ToList();

    /// <summary>
    /// The primary owner has been erased (POPIA, AS-03) but the item is shared: hand it to the longest-standing
    /// household co-owner, who owns it equally, rather than take it out of their ledger. Their co-ownership
    /// becomes primary ownership; the rest of the household keeps its share. Returns the new owner, or
    /// <c>null</c> when there is no co-owner to receive it (the caller then erases the item).
    /// </summary>
    public SubjectId? HandOverToCoOwner(DateTimeOffset at)
    {
        var successor = _coOwners.OrderBy(c => c.AddedAt).FirstOrDefault();
        if (successor is null)
        {
            return null;
        }

        var from = OwnerId;
        _coOwners.Remove(successor);
        OwnerId = successor.MemberId;
        RaiseDomainEvent(new AssetTransferredDomainEvent(Id, from, successor.MemberId, at));
        return successor.MemberId;
    }

    /// <summary>
    /// Add a household member as a co-owner (deck §31), so the shared item appears in their ledger too.
    /// Idempotent; the primary owner cannot be added as their own co-owner.
    /// </summary>
    public void AddCoOwner(SubjectId member, DateTimeOffset at)
    {
        if (member == OwnerId || _coOwners.Any(c => c.MemberId == member))
        {
            return;
        }

        _coOwners.Add(AssetCoOwner.Add(member, at));
        RaiseDomainEvent(new AssetCoOwnerAddedDomainEvent(Id, member, at));
    }

    /// <summary>Remove a household co-owner. A no-op if they are not a co-owner (the primary owner is never one).</summary>
    public void RemoveCoOwner(SubjectId member, DateTimeOffset at)
    {
        var existing = _coOwners.FirstOrDefault(c => c.MemberId == member);
        if (existing is null)
        {
            return;
        }

        _coOwners.Remove(existing);
        RaiseDomainEvent(new AssetCoOwnerRemovedDomainEvent(Id, member, at));
    }

    /// <summary>The owner reports the item stolen (deck §30). Only an active item can be reported stolen.</summary>
    public void ReportStolen(DateTimeOffset reportedAt)
    {
        if (Status == AssetLifecycleStatus.ReportedStolen)
        {
            throw new DomainException("Asset is already reported stolen.");
        }

        Status = AssetLifecycleStatus.ReportedStolen;
        RaiseDomainEvent(new AssetReportedStolenDomainEvent(Id, OwnerId, reportedAt));
    }

    /// <summary>The item is recovered and returns to active ownership (deck §30). Only a stolen item recovers.</summary>
    public void Recover(DateTimeOffset recoveredAt)
    {
        if (Status != AssetLifecycleStatus.ReportedStolen)
        {
            throw new DomainException("Only an asset reported stolen can be recovered.");
        }

        Status = AssetLifecycleStatus.Active;
        RaiseDomainEvent(new AssetRecoveredDomainEvent(Id, OwnerId, recoveredAt));
    }
}
