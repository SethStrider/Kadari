using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.AddCaseCommitment;

/// <summary>A customer may only add a promise on their own assets.</summary>
internal sealed class AddCaseCommitmentOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<AddCaseCommitmentCommand>
{
    public Task<ResourceOwner?> ResolveAsync(AddCaseCommitmentCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
