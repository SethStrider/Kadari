using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Assets.RecordPurchaseDetails;

/// <summary>A customer may only record purchase details on their own asset — the command names the customer.</summary>
internal sealed class RecordPurchaseDetailsOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<RecordPurchaseDetailsCommand>
{
    public Task<ResourceOwner?> ResolveAsync(RecordPurchaseDetailsCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
