using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Features.SubmitValuation;

/// <summary>
/// A professional valuer submits an independent valuation for an asset (deck §18, D10) — the human-in-the-loop
/// rung the AI baseline cannot reach. It attaches independent Value evidence, upgrading the asset's assurance
/// to ValueSupported once ownership is also independently supported. Resource-scoped on the asset's owner, but
/// the valuer is staff (Verifier), who bypasses ownership — a valuer is not the owner of what they appraise.
/// </summary>
public sealed record SubmitAssetValuationCommand(
    Guid AssetId, long AmountMinor, string Currency, string Valuer, string CertificateReference)
    : ICommand<SubmitAssetValuationResponse>, IResourceScopedRequest
{
    public Guid ResourceId => AssetId;
}

public sealed record SubmitAssetValuationResponse(Guid AssetId, string Assurance);

internal sealed class SubmitAssetValuationHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<SubmitAssetValuationCommand, SubmitAssetValuationResponse>
{
    public async Task<SubmitAssetValuationResponse> Handle(SubmitAssetValuationCommand cmd, CancellationToken ct)
    {
        var assetId = new AssetId(cmd.AssetId);
        var asset = await db.Assets
            .Include(a => a.Evidence)
            .FirstOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new NotFoundException(nameof(Asset), cmd.AssetId);

        var money = new Money(cmd.AmountMinor, cmd.Currency);
        asset.Attach(AssetEvidence.ProfessionallyValued(
            cmd.Valuer.Trim(), money, cmd.CertificateReference.Trim(), clock.GetUtcNow()));

        await db.SaveChangesAsync(ct);

        return new SubmitAssetValuationResponse(asset.Id.Value, asset.Assurance.ToString());
    }
}
