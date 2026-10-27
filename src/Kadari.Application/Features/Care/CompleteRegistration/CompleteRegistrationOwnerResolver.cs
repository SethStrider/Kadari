using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.CompleteRegistration;

/// <summary>A customer may only confirm registrations on their own asset — the command names the customer.</summary>
internal sealed class CompleteRegistrationOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<CompleteRegistrationCommand>
{
    public Task<ResourceOwner?> ResolveAsync(CompleteRegistrationCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
