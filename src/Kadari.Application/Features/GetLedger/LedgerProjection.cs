using Kadari.Domain.Assets;

namespace Kadari.Application.Features.GetLedger;

/// <summary>
/// Pure projection of a set of <see cref="Asset"/> aggregates into the Live Asset Ledger read model
/// (deck §14). Kept free of EF/I/O so it is unit-testable with in-memory aggregates; the handler loads
/// the assets and hands them here.
/// </summary>
public static class LedgerProjection
{
    private const string DefaultCurrency = "ZAR";

    /// <summary>The room label used when an asset was captured without one.</summary>
    public const string UnassignedRoom = "Unassigned";

    public static LedgerDto From(Guid customerId, IReadOnlyCollection<Asset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var currency = assets
            .Select(BestValue)
            .FirstOrDefault(v => v is not null)?.Currency ?? DefaultCurrency;

        var rooms = assets
            .GroupBy(a => string.IsNullOrWhiteSpace(a.RoomLabel) ? UnassignedRoom : a.RoomLabel!)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LedgerRoomDto(
                g.Key,
                g.Sum(a => BestValueMinor(a, currency)),
                g.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(ToAssetDto)
                    .ToList()))
            .ToList();

        var exposure = rooms.Sum(r => r.RoomTotalMinor);
        var valued = assets.Count(a => BestValue(a) is not null);
        var completeness = assets.Count == 0 ? 0 : (int)Math.Round(100.0 * valued / assets.Count);

        return new LedgerDto(customerId, assets.Count, exposure, currency, completeness, rooms);
    }

    private static LedgerAssetDto ToAssetDto(Asset asset)
    {
        var value = asset.BestReplacementValue;
        return new LedgerAssetDto(
            asset.Id.Value, asset.DisplayName, asset.Category.ToString(), asset.Assurance.ToString(),
            value?.AmountMinor, asset.Status.ToString());
    }

    private static long BestValueMinor(Asset asset, string currency)
    {
        var value = asset.BestReplacementValue;
        return value is not null && value.Currency == currency ? value.AmountMinor : 0;
    }

    private static Domain.ValueObjects.Money? BestValue(Asset asset) => asset.BestReplacementValue;
}
