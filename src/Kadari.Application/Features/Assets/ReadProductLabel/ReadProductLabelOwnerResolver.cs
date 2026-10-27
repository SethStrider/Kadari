using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Assets.ReadProductLabel;

/// <summary>A customer may only have labels read for their own asset — the command names the customer.</summary>
internal sealed class ReadProductLabelOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<ReadProductLabelCommand>
{
    public Task<ResourceOwner?> ResolveAsync(ReadProductLabelCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
