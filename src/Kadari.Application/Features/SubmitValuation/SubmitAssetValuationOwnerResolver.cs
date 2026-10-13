using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.SubmitValuation;

/// <summary>
/// Resolves the asset to its owning customer so the pipeline can settle existence (a missing asset → 404,
/// not 403) and, for a non-staff caller, enforce ownership. The valuer is a Verifier (staff), who bypasses
/// ownership — so this gate's job here is really the 404, while the AssetValuate permission is the constraint.
/// </summary>
internal sealed class SubmitAssetValuationOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<SubmitAssetValuationCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(SubmitAssetValuationCommand request, CancellationToken cancellationToken)
    {
        var assetId = new AssetId(request.AssetId);

        var ownerId = await db.Assets.AsNoTracking()
            .Where(a => a.Id == assetId)
            .Select(a => (SubjectId?)a.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId is null)
        {
            return null;   // no such asset → 404
        }

        var customerId = await db.Subjects.AsNoTracking()
            .Where(s => s.Id == ownerId.Value)
            .Select(s => s.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);

        return customerId is { } cid ? ResourceOwner.Customer(cid.Value) : null;
    }
}
