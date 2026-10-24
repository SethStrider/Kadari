using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.EnableCare;

/// <summary>A customer may only enable Care on their own asset — the command names the customer.</summary>
internal sealed class EnableCareOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<EnableCareCommand>
{
    public Task<ResourceOwner?> ResolveAsync(EnableCareCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
