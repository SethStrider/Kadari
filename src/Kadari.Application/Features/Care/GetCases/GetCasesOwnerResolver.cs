using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.GetCases;

/// <summary>A customer may only read cases on their own assets.</summary>
internal sealed class GetCasesOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetCasesQuery>
{
    public Task<ResourceOwner?> ResolveAsync(GetCasesQuery request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
