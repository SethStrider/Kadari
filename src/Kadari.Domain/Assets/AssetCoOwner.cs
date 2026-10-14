using Kadari.Domain.Abstractions;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Assets;

public readonly record struct AssetCoOwnerId(Guid Value)
{
    public static AssetCoOwnerId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// A household co-owner of an asset (deck §31): a subject who shares ownership alongside the primary
/// <see cref="Asset.OwnerId"/>. A child of the Asset aggregate — membership is managed only through the
/// aggregate — so an asset shared with household members appears in each member's ledger without duplicating
/// the item. Kept minimal: who, and since when.
/// </summary>
public sealed class AssetCoOwner : Entity<AssetCoOwnerId>
{
    private AssetCoOwner() { }   // EF

    private AssetCoOwner(AssetCoOwnerId id, OwnerId memberId, DateTimeOffset addedAt) : base(id)
    {
        MemberId = memberId;
        AddedAt = addedAt;
    }

    public OwnerId MemberId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    internal static AssetCoOwner Add(OwnerId memberId, DateTimeOffset addedAt) =>
        new(AssetCoOwnerId.New(), memberId, addedAt);
}
