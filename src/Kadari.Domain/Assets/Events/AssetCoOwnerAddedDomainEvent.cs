using Kadari.Domain.Abstractions;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when a household member is added as a co-owner of an asset (deck §31).</summary>
public sealed record AssetCoOwnerAddedDomainEvent(AssetId AssetId, OwnerId MemberId, DateTimeOffset At)
    : IDomainEvent;

/// <summary>Raised when a household member is removed as a co-owner of an asset.</summary>
public sealed record AssetCoOwnerRemovedDomainEvent(AssetId AssetId, OwnerId MemberId, DateTimeOffset At)
    : IDomainEvent;
