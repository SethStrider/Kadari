using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.ResolveCase;

/// <summary>A customer may only close a case on their own assets.</summary>
internal sealed class ResolveCaseOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<ResolveCaseCommand>
{
    public Task<ResourceOwner?> ResolveAsync(ResolveCaseCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
