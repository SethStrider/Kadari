using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.Services;

/// <summary>Raises Care Ops work, once per exception (by its dedupe key — unique in the database, and checked against
/// what this transaction already added), and closes a case's open work when the case no longer needs a person.</summary>
internal sealed class OpsQueue(IKadariDbContext db, TimeProvider clock)
{
    public async Task RaiseAsync(
        OpsWorkType type, OpsSeverity severity, AssetId? assetId, AssetCaseId? caseId, Guid sourceId, string reasonCode,
        string summary, CancellationToken ct)
    {
        var key = OpsWorkItem.KeyFor(type, sourceId);
        if (db.Set<OpsWorkItem>().Local.Any(w => w.DedupeKey == key)
            || await db.Set<OpsWorkItem>().AnyAsync(w => w.DedupeKey == key, ct))
        {
            return;
        }

        db.Set<OpsWorkItem>().Add(OpsWorkItem.Raise(type, severity, assetId, caseId, sourceId, reasonCode, summary, clock.GetUtcNow()));
    }

    public async Task CloseForCaseAsync(AssetCaseId caseId, string why, CancellationToken ct)
    {
        var open = await db.Set<OpsWorkItem>()
            .Where(w => w.CaseId == caseId && w.Status != OpsWorkStatus.Resolved)
            .ToListAsync(ct);
        foreach (var item in open)
        {
            item.Close(why, clock.GetUtcNow());
        }
    }
}
