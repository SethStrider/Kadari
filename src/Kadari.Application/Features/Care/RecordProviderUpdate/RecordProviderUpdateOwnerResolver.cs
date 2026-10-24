using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.RecordProviderUpdate;

/// <summary>A customer may only record a provider update on their own assets.</summary>
internal sealed class RecordProviderUpdateOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<RecordProviderUpdateCommand>
{
    public Task<ResourceOwner?> ResolveAsync(RecordProviderUpdateCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
