using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Assets.RecordPurchaseDetails;

/// <summary>
/// Record what the owner bought, where and when — the facts KADARI Care's coverage engine reads (CARE/14 C3, manual
/// entry first; extraction proposals arrive later and land here once the owner confirms them). Appended as
/// <see cref="AssetEvidenceKind.Purchase"/> evidence: a correction is a new item, so nothing the owner confirmed
/// earlier is overwritten. When the asset already holds a stored receipt, the facts cite it and count as independent.
/// Customer-scoped — the owner or a household co-owner.
/// </summary>
public sealed record RecordPurchaseDetailsCommand(
    Guid CustomerId,
    Guid AssetId,
    string? Brand,
    string? Model,
    string? Retailer,
    DateOnly? PurchasedOn) : ICommand<RecordPurchaseDetailsResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record RecordPurchaseDetailsResponse(Guid AssetId, Guid EvidenceId, bool BackedByReceipt);

internal sealed class RecordPurchaseDetailsHandler(IKadariDbContext db, OwnedAssets assets, TimeProvider clock)
    : ICommandHandler<RecordPurchaseDetailsCommand, RecordPurchaseDetailsResponse>
{
    /// <summary>The source tag for facts the owner typed or confirmed.</summary>
    public const string OwnerSource = "owner";

    public async Task<RecordPurchaseDetailsResponse> Handle(RecordPurchaseDetailsCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.CustomerId, cmd.AssetId, ct);

        var receipt = asset.Evidence
            .Where(e => e.Kind == AssetEvidenceKind.Ownership && e.Strength == EvidenceStrength.Independent
                        && e.Source == "receipt" && e.Reference != null)
            .OrderByDescending(e => e.AssertedAt)
            .Select(e => e.Reference)
            .FirstOrDefault();

        var evidence = asset.Attach(AssetEvidence.PurchaseRecorded(
            OwnerSource, new PurchaseFacts(cmd.Brand, cmd.Model, cmd.Retailer, cmd.PurchasedOn), clock.GetUtcNow(), receipt));

        await db.SaveChangesAsync(ct);

        return new RecordPurchaseDetailsResponse(asset.Id.Value, evidence.Id.Value, receipt is not null);
    }
}
