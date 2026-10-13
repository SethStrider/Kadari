using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when a piece of evidence is attached to an asset (may change its derived assurance).</summary>
public sealed record AssetEvidenceAttachedDomainEvent(
    AssetId AssetId, AssetEvidenceId EvidenceId, AssetEvidenceKind Kind) : IDomainEvent;
