using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Reports;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.Exit;

/// <summary>
/// Produce the signed "asset-for-sale" export for one of a customer's assets (spec §7) — the one-way handoff a
/// marketplace (PoziSnap) imports to pre-fill a listing with Trustli-verified provenance. Customer-scoped: a
/// caller may only export their own asset. Requires a minted Asset Passport (the provenance the payload
/// carries); until one exists it is a 404, so the client mints then exports.
/// </summary>
public sealed record ExportAssetForSaleQuery(Guid CustomerId, Guid AssetId)
    : IQuery<AssetForSaleExportDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

/// <summary>The export: the human-readable attributes plus the self-contained, verifiable <see cref="Token"/>.</summary>
public sealed record AssetForSaleExportDto(AssetForSalePackage Package, string Token);

internal sealed class ExportAssetForSaleHandler(IKadariDbContext db, IReportIntegritySigner signer, TimeProvider clock)
    : IQueryHandler<ExportAssetForSaleQuery, AssetForSaleExportDto>
{
    public async Task<AssetForSaleExportDto> Handle(ExportAssetForSaleQuery query, CancellationToken ct)
    {
        var customerId = new CustomerId(query.CustomerId);

        var subjectId = await db.Subjects.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (SubjectId?)s.Id)
            .FirstOrDefaultAsync(ct);

        var assetId = new AssetId(query.AssetId);
        var asset = subjectId is null
            ? null
            : await db.Assets.AsNoTracking()
                .Include(a => a.Evidence)
                .FirstOrDefaultAsync(a => a.Id == assetId && a.OwnerId == subjectId.Value, ct);

        if (asset is null)
        {
            throw new NotFoundException(nameof(Asset), query.AssetId);
        }

        // Provenance is the point of the export, so a passport must have been minted first.
        var passport = await db.AssetPassports.AsNoTracking()
            .FirstOrDefaultAsync(p => p.AssetId == assetId, ct)
            ?? throw new NotFoundException(nameof(AssetPassport), query.AssetId);

        var image = asset.Evidence
            .Where(e => e.Kind == AssetEvidenceKind.Image && e.Reference != null)
            .OrderByDescending(e => e.AssertedAt)
            .Select(e => e.Reference)
            .FirstOrDefault();

        var value = asset.BestReplacementValue;
        var package = new AssetForSalePackage(
            asset.Id.Value,
            passport.VerificationId,
            asset.DisplayName,
            asset.Category.ToString(),
            asset.Assurance.ToString(),
            value?.AmountMinor,
            value?.Currency,
            image,
            clock.GetUtcNow());

        return new AssetForSaleExportDto(package, AssetExportProtocol.Encode(package, signer));
    }
}
