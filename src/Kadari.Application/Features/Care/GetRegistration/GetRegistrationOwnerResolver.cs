using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.GetRegistration;

/// <summary>A customer may only read registrations for their own asset — the query names the customer.</summary>
internal sealed class GetRegistrationOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetRegistrationQuery>
{
    public Task<ResourceOwner?> ResolveAsync(GetRegistrationQuery request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
