using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.KeepCaseCommitment;

/// <summary>A customer may only settle a promise on their own assets.</summary>
internal sealed class KeepCaseCommitmentOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<KeepCaseCommitmentCommand>
{
    public Task<ResourceOwner?> ResolveAsync(KeepCaseCommitmentCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
