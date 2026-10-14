using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Tenancy;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;

namespace Kadari.Application.Features.GetAssetPassport;

/// <summary>
/// Read an asset's Asset Passport (deck §16). Customer-scoped — a caller may only read a passport for an
/// asset in their own ledger. A 404 is returned when the asset is not theirs or no passport has been minted
/// yet (minting is an explicit action — see MintAssetPassport).
/// </summary>
public sealed record GetAssetPassportQuery(Guid CustomerId, Guid AssetId)
    : IQuery<AssetPassportDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

/// <summary>The externally-verifiable Asset Passport payload (spec D9 — verifiable/exportable from day one).</summary>
public sealed record AssetPassportDto(
    Guid PassportId,
    Guid AssetId,
    string VerificationId,
    string Status,
    string DisplayName,
    string Category,
    string Assurance,
    long? ReplacementValueMinor,
    string? Currency,
    int EvidenceCount,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastUpdatedAt)
{
    public static AssetPassportDto From(AssetPassport p) => new(
        p.Id.Value, p.AssetId.Value, p.VerificationId, p.Status.ToString(), p.DisplayName,
        p.Category.ToString(), p.Assurance.ToString(), p.ReplacementValue?.AmountMinor,
        p.ReplacementValue?.Currency, p.EvidenceCount, p.IssuedAt, p.LastUpdatedAt);
}

internal sealed class GetAssetPassportHandler(IKadariDbContext db, ICurrentTenant tenant)
    : IQueryHandler<GetAssetPassportQuery, AssetPassportDto>
{
    public async Task<AssetPassportDto> Handle(GetAssetPassportQuery query, CancellationToken ct)
    {
        var customerId = new CustomerId(query.CustomerId);

        var subjectId = await db.Owners.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (OwnerId?)s.Id)
            .FirstOrDefaultAsync(ct);

        var assetId = new AssetId(query.AssetId);

        // Ownership is checked on the asset (primary owner OR a household co-owner), not the passport's
        // denormalised owner — so a co-owner may read the shared asset's credential too (deck §31). A caller
        // who owns neither simply gets a 404, not a 403.
        var owns = subjectId is not null && await db.Assets.AsNoTracking()
            .AnyAsync(a => a.Id == assetId
                && (a.OwnerId == subjectId.Value || a.CoOwners.Any(c => c.MemberId == subjectId.Value)), ct);

        var passport = owns
            ? await db.AssetPassports.AsNoTracking()
                .Where(p => p.AssetId == assetId)
                .ForCurrentTenant(tenant)   // defence-in-depth, the insurer-console seam
                .FirstOrDefaultAsync(ct)
            : null;

        if (passport is null)
        {
            throw new NotFoundException(nameof(AssetPassport), query.AssetId);
        }

        return AssetPassportDto.From(passport);
    }
}
