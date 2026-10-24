using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Assets.ReadReceipt;

/// <summary>A customer may only have their own asset's receipt read — the command names the customer.</summary>
internal sealed class ReadReceiptOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<ReadReceiptCommand>
{
    public Task<ResourceOwner?> ResolveAsync(ReadReceiptCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
