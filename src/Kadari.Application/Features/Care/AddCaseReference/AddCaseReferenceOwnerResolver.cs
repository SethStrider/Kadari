using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.AddCaseReference;

/// <summary>A customer may only add a reference on their own assets.</summary>
internal sealed class AddCaseReferenceOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<AddCaseReferenceCommand>
{
    public Task<ResourceOwner?> ResolveAsync(AddCaseReferenceCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
