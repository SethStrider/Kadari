using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.WithdrawCase;

/// <summary>A customer may only withdraw a case on their own assets.</summary>
internal sealed class WithdrawCaseOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<WithdrawCaseCommand>
{
    public Task<ResourceOwner?> ResolveAsync(WithdrawCaseCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
