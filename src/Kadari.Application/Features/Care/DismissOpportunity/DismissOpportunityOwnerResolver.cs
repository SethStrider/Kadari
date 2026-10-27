using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.DismissOpportunity;

/// <summary>A customer may only dismiss offers on their own asset.</summary>
internal sealed class DismissOpportunityOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<DismissOpportunityCommand>
{
    public Task<ResourceOwner?> ResolveAsync(DismissOpportunityCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
