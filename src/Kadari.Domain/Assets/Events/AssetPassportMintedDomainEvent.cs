using Kadari.Domain.Abstractions;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when an Asset Passport is first minted for an asset.</summary>
public sealed record AssetPassportMintedDomainEvent(AssetPassportId PassportId, AssetId AssetId, OwnerId OwnerId)
    : IDomainEvent;
