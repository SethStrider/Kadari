using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Subjects.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.TransferAsset;

/// <summary>
/// Transfer one of a customer's assets to another customer — a gift or private sale (deck §31). Ownership is a
/// re-point of the asset (and its passport), so the item's evidence and history carry across. Customer-scoped:
/// only the current owner may give their own asset away. The asset keeps its tenant partition; only the owner
/// changes.
/// </summary>
public sealed record TransferAssetCommand(Guid CustomerId, Guid AssetId, Guid RecipientCustomerId)
    : ICommand<TransferAssetResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record TransferAssetResponse(Guid AssetId, Guid NewOwnerCustomerId);

internal sealed class TransferAssetHandler(IKadariDbContext db, SubjectRegistrar subjects, TimeProvider clock)
    : ICommandHandler<TransferAssetCommand, TransferAssetResponse>
{
    public async Task<TransferAssetResponse> Handle(TransferAssetCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);
        var recipientId = new CustomerId(cmd.RecipientCustomerId);
        var now = clock.GetUtcNow();

        if (!await db.Set<Customer>().AnyAsync(c => c.Id == recipientId, ct))
        {
            throw new NotFoundException(nameof(Customer), cmd.RecipientCustomerId);
        }

        var ownerSubjectId = await db.Subjects.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (SubjectId?)s.Id)
            .FirstOrDefaultAsync(ct);

        var assetId = new AssetId(cmd.AssetId);
        var asset = ownerSubjectId is null
            ? null
            : await db.Assets
                .Include(a => a.CoOwners)   // loaded so Transfer's co-owner clear is tracked and persisted
                .FirstOrDefaultAsync(a => a.Id == assetId && a.OwnerId == ownerSubjectId.Value, ct);

        if (asset is null)
        {
            throw new NotFoundException(nameof(Asset), cmd.AssetId);
        }

        var recipientSubject = await subjects.GetOrCreateForCustomerAsync(recipientId, ct);

        asset.Transfer(recipientSubject, now);

        // Keep the passport's denormalised owner in step so the recipient's scoped reads resolve it.
        var passport = await db.AssetPassports.FirstOrDefaultAsync(p => p.AssetId == assetId, ct);
        passport?.ReassignOwner(recipientSubject, now);

        await db.SaveChangesAsync(ct);

        return new TransferAssetResponse(cmd.AssetId, cmd.RecipientCustomerId);
    }
}
