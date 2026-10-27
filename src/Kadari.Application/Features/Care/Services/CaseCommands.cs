using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

/// <summary>What every case command does around its one change: find the asset in the customer's ledger, load the case
/// tracked, and after the change save and answer with the case as the owner now sees it.</summary>
internal sealed class CaseCommands(IKadariDbContext db, OwnedAssets assets, TimeProvider clock)
{
    public DateTimeOffset Now => clock.GetUtcNow();

    public async Task<(Asset Asset, AssetCase Case)> LoadAsync(Guid customerId, Guid assetId, Guid caseId, CancellationToken ct)
    {
        var asset = await assets.GetAsync(customerId, assetId, ct);
        return (asset, await CaseView.GetAsync(db, asset, caseId, tracked: true, ct));
    }

    public async Task<CaseDto> SaveAsync(Asset asset, AssetCase @case, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        return CaseView.Detail(@case, asset, DateOnly.FromDateTime(Now.UtcDateTime));
    }
}
