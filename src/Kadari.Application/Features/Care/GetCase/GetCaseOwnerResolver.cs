using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.GetCase;

/// <summary>A customer may only read a case on their own assets.</summary>
internal sealed class GetCaseOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetCaseQuery>
{
    public Task<ResourceOwner?> ResolveAsync(GetCaseQuery request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
