using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.MarkCaseSent;

/// <summary>A customer may only send a case on their own assets.</summary>
internal sealed class MarkCaseSentOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<MarkCaseSentCommand>
{
    public Task<ResourceOwner?> ResolveAsync(MarkCaseSentCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
