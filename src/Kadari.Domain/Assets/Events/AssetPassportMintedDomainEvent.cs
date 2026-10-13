using Kadari.Domain.Abstractions;
using Kadari.Domain.Subjects;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when an Asset Passport is first minted for an asset.</summary>
public sealed record AssetPassportMintedDomainEvent(AssetPassportId PassportId, AssetId AssetId, SubjectId OwnerId)
    : IDomainEvent;
