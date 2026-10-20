using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Tenancy;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;

namespace Kadari.Application.Features.GetAsset;

/// <summary>
/// Read one asset in a customer's ledger, with its full evidence set (deck §14). Customer-scoped — a caller
/// may only read their own assets; the owning customer is <see cref="ResourceId"/>. A 404 (not 403) is
/// returned when the asset is not in that customer's ledger, so ids are not leaked.
/// </summary>
public sealed record GetAssetQuery(Guid CustomerId, Guid AssetId) : IQuery<AssetDetailDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record AssetDetailDto(
    Guid AssetId,
    string DisplayName,
    string Category,
    string Assurance,
    string Status,
    string? RoomLabel,
    long? BestValueMinor,
    string? Currency,
    DateTimeOffset RegisteredAt,
    IReadOnlyList<AssetEvidenceDto> Evidence);

public sealed record AssetEvidenceDto(
    string Kind, string Strength, string Source, DateTimeOffset AssertedAt,
    double? Confidence, long? AmountMinor, string? Currency);

internal sealed class GetAssetHandler(IKadariDbContext db)
    : IQueryHandler<GetAssetQuery, AssetDetailDto>
{
    public async Task<AssetDetailDto> Handle(GetAssetQuery query, CancellationToken ct)
    {
        var customerId = new CustomerId(query.CustomerId);
        var tenant = await db.TenantOfCustomerAsync(customerId, ct);

        var subjectId = await db.Owners.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (OwnerId?)s.Id)
            .FirstOrDefaultAsync(ct);

        var assetId = new AssetId(query.AssetId);
        var asset = subjectId is null
            ? null
            : await db.Assets.AsNoTracking()
                .Include(a => a.Evidence)
                .Where(a => a.Id == assetId
                    && (a.OwnerId == subjectId.Value || a.CoOwners.Any(c => c.MemberId == subjectId.Value)))
                .ForTenant(tenant)
                .FirstOrDefaultAsync(ct);

        if (asset is null)
        {
            throw new NotFoundException(nameof(Asset), query.AssetId);
        }

        var best = asset.BestReplacementValue;
        var evidence = asset.Evidence
            .OrderBy(e => e.AssertedAt)
            .Select(e => new AssetEvidenceDto(
                e.Kind.ToString(), e.Strength.ToString(), e.Source, e.AssertedAt,
                e.Confidence, e.Amount?.AmountMinor, e.Amount?.Currency))
            .ToList();

        return new AssetDetailDto(
            asset.Id.Value, asset.DisplayName, asset.Category.ToString(), asset.Assurance.ToString(),
            asset.Status.ToString(), asset.RoomLabel, best?.AmountMinor, best?.Currency,
            asset.RegisteredAt, evidence);
    }
}
