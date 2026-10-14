using Kadari.Domain.Abstractions;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when a stolen asset is recovered and returns to active ownership (deck §30).</summary>
public sealed record AssetRecoveredDomainEvent(AssetId AssetId, OwnerId OwnerId, DateTimeOffset RecoveredAt)
    : IDomainEvent;
