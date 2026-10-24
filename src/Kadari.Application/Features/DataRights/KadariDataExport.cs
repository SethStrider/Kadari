using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.DataRights;

public sealed record KadariEvidenceExport(
    string Kind, string Strength, string Source, DateTimeOffset AssertedAt,
    long? AmountMinor, string? Currency, bool HasFile,
    string? PurchaseBrand, string? PurchaseModel, string? PurchaseRetailer, DateOnly? PurchasedOn);

public sealed record KadariAssetExport(
    Guid AssetId, string DisplayName, string Category, string? RoomLabel, string Status, string Assurance,
    bool PrimaryOwner, DateTimeOffset RegisteredAt, IReadOnlyList<KadariEvidenceExport> Evidence);

public sealed record KadariPassportExport(Guid AssetId, string VerificationId, string Status, DateTimeOffset IssuedAt);

public sealed record KadariCareExport(
    Guid AssetId, string Category, string CoverageState, DateOnly? CoverageExpiresOn, DateTimeOffset EnabledAt);

public sealed record KadariHistoryExport(Guid AssetId, string Kind, string Summary, DateTimeOffset OccurredAt);

public sealed record KadariConsentExport(string Scope, string Status, DateTimeOffset GrantedAt, DateTimeOffset? WithdrawnAt);

public sealed record KadariDataExportDto(
    string? Insurer,
    IReadOnlyList<KadariAssetExport> Assets,
    IReadOnlyList<KadariPassportExport> Passports,
    IReadOnlyList<KadariConsentExport> Consents,
    IReadOnlyList<KadariCareExport> Care,
    IReadOnlyList<KadariHistoryExport> History);

/// <summary>
/// The Kadari part of a customer's POPIA subject-access export (AS-03): the insurer they are with, every asset
/// they own or share with its evidence trail (purchase details included), their Asset Passports, their Kadari
/// consents, KADARI Care on their assets and each asset's history. Evidence files
/// (photos, receipts) are listed, not embedded — <c>HasFile</c> says one is held, and staff retrieve it on request.
/// </summary>
public sealed class KadariDataExport(IKadariDbContext db)
{
    public async Task<KadariDataExportDto> ExportAsync(CustomerId customerId, CancellationToken ct)
    {
        var consents = await db.KadariConsents.AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new KadariConsentExport(c.Scope.ToString(), c.Status.ToString(), c.GrantedAt, c.WithdrawnAt))
            .ToListAsync(ct);

        var insurer = await db.TenantMemberships.AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .Join(db.Tenants.AsNoTracking(), m => m.TenantId, t => t.Id, (_, t) => t.Name)
            .FirstOrDefaultAsync(ct);

        var subject = await db.Owners.AsNoTracking().FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);
        if (subject is null)
        {
            return new KadariDataExportDto(insurer, [], [], consents, [], []);
        }

        var subjectId = subject.Id;
        var assets = await db.Assets.AsNoTracking()
            .Include(a => a.Evidence)
            .Where(a => a.OwnerId == subjectId || a.CoOwners.Any(c => c.MemberId == subjectId))
            .ToListAsync(ct);

        var assetIds = assets.Select(a => a.Id).ToList();
        var passports = await db.AssetPassports.AsNoTracking()
            .Where(p => assetIds.Contains(p.AssetId))
            .Select(p => new KadariPassportExport(p.AssetId.Value, p.VerificationId, p.Status.ToString(), p.IssuedAt))
            .ToListAsync(ct);

        var care = await db.CareProfiles.AsNoTracking()
            .Where(p => assetIds.Contains(p.AssetId))
            .Select(p => new KadariCareExport(
                p.AssetId.Value, p.Category.ToString(), p.CoverageState.ToString(), p.CoverageExpiresOn, p.EnabledAt))
            .ToListAsync(ct);

        var history = await db.AssetHistory.AsNoTracking()
            .Where(h => assetIds.Contains(h.AssetId))
            .OrderBy(h => h.OccurredAt)
            .Select(h => new KadariHistoryExport(h.AssetId.Value, h.Kind.ToString(), h.Summary, h.OccurredAt))
            .ToListAsync(ct);

        var assetExports = assets
            .Select(a => new KadariAssetExport(
                a.Id.Value, a.DisplayName, a.Category.ToString(), a.RoomLabel, a.Status.ToString(),
                a.Assurance.ToString(), a.OwnerId == subjectId, a.RegisteredAt,
                a.Evidence
                    .OrderBy(e => e.AssertedAt)
                    .Select(e => new KadariEvidenceExport(
                        e.Kind.ToString(), e.Strength.ToString(), e.Source, e.AssertedAt,
                        e.Amount?.AmountMinor, e.Amount?.Currency, e.Reference is not null,
                        e.Purchase?.Brand, e.Purchase?.Model, e.Purchase?.Retailer, e.Purchase?.PurchasedOn))
                    .ToList()))
            .ToList();

        return new KadariDataExportDto(insurer, assetExports, passports, consents, care, history);
    }
}
