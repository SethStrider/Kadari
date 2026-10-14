using Kadari.Domain.Abstractions;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when a new asset is registered into a subject's ledger.</summary>
public sealed record AssetRegisteredDomainEvent(AssetId AssetId, OwnerId OwnerId, AssetCategory Category)
    : IDomainEvent;
