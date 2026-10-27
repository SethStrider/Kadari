using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.EvaluateCare;

/// <summary>A customer may only re-evaluate Care on their own asset — the command names the customer.</summary>
internal sealed class EvaluateCareOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<EvaluateCareCommand>
{
    public Task<ResourceOwner?> ResolveAsync(EvaluateCareCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
