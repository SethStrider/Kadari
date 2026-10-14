using Kadari.Domain.Abstractions;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when an asset changes hands by transfer (a gift or sale within/between households, deck §31).</summary>
public sealed record AssetTransferredDomainEvent(AssetId AssetId, OwnerId From, OwnerId To, DateTimeOffset At)
    : IDomainEvent;
