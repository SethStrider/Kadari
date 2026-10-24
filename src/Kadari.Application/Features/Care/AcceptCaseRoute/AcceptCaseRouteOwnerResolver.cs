using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.AcceptCaseRoute;

/// <summary>A customer may only confirm a route on their own assets.</summary>
internal sealed class AcceptCaseRouteOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<AcceptCaseRouteCommand>
{
    public Task<ResourceOwner?> ResolveAsync(AcceptCaseRouteCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
