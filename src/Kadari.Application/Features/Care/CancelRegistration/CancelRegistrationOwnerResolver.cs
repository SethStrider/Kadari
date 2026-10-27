using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.CancelRegistration;

/// <summary>A customer may only cancel registrations on their own asset.</summary>
internal sealed class CancelRegistrationOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<CancelRegistrationCommand>
{
    public Task<ResourceOwner?> ResolveAsync(CancelRegistrationCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
