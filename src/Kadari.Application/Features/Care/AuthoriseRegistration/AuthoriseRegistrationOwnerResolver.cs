using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.AuthoriseRegistration;

/// <summary>A customer may only authorise registrations for their own asset — the command names the customer.</summary>
internal sealed class AuthoriseRegistrationOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<AuthoriseRegistrationCommand>
{
    public Task<ResourceOwner?> ResolveAsync(AuthoriseRegistrationCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
