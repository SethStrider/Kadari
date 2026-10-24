using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.EvaluateCare;

/// <summary>
/// Re-run the Coverage Engine for an asset against its latest evidence and the current published rules (CARE/13 §3).
/// Every run appends a new decision; earlier decisions — and the rule versions they cite — are never touched.
/// Customer-scoped; 404 when Care is not enabled on the asset.
/// </summary>
public sealed record EvaluateCareCommand(Guid CustomerId, Guid AssetId) : ICommand<EvaluateCareResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record EvaluateCareResponse(Guid AssetId, Guid CoverageDecisionId, string CoverageState);

internal sealed class EvaluateCareHandler(IKadariDbContext db, OwnedAssets assets, CareCoverage coverage)
    : ICommandHandler<EvaluateCareCommand, EvaluateCareResponse>
{
    public async Task<EvaluateCareResponse> Handle(EvaluateCareCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.CustomerId, cmd.AssetId, ct);
        var profile = await db.CareProfiles.Include(p => p.Coverage).FirstOrDefaultAsync(p => p.AssetId == asset.Id, ct)
            ?? throw new NotFoundException(nameof(CareProfile), cmd.AssetId);

        var decision = await coverage.EvaluateAsync(asset, profile, ct);
        await db.SaveChangesAsync(ct);

        return new EvaluateCareResponse(asset.Id.Value, decision.Id.Value, decision.ResultState.ToString());
    }
}
