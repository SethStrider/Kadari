using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Assets.GetAssetHistory;

/// <summary>
/// Read an asset's permanent history, newest first (CARE/05 C28): registration, transfers, theft and recovery, and
/// every material Care outcome. One history for the asset, whichever capability wrote the entry. Customer-scoped.
/// </summary>
public sealed record GetAssetHistoryQuery(Guid CustomerId, Guid AssetId) : IQuery<AssetHistoryDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record AssetHistoryDto(Guid AssetId, IReadOnlyList<AssetHistoryEntryDto> Entries);

public sealed record AssetHistoryEntryDto(
    Guid Id, string Kind, string Summary, DateTimeOffset OccurredAt, string? SourceType, Guid? SourceId);

internal sealed class GetAssetHistoryHandler(IKadariDbContext db, OwnedAssets assets)
    : IQueryHandler<GetAssetHistoryQuery, AssetHistoryDto>
{
    /// <summary>An asset's life story is short; the cap only guards the response size.</summary>
    private const int MaxEntries = 500;

    public async Task<AssetHistoryDto> Handle(GetAssetHistoryQuery query, CancellationToken ct)
    {
        var asset = await assets.GetAsync(query.CustomerId, query.AssetId, ct);

        var entries = await db.AssetHistory.AsNoTracking()
            .Where(h => h.AssetId == asset.Id)
            .OrderByDescending(h => h.OccurredAt)
            .ThenByDescending(h => h.Id)
            .Take(MaxEntries)
            .Select(h => new AssetHistoryEntryDto(
                h.Id.Value, h.Kind.ToString(), h.Summary, h.OccurredAt, h.SourceType, h.SourceId))
            .ToListAsync(ct);

        return new AssetHistoryDto(asset.Id.Value, entries);
    }
}
