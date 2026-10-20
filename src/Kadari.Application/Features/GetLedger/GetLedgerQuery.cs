using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Tenancy;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;

namespace Kadari.Application.Features.GetLedger;

/// <summary>
/// Read a customer's Live Asset Ledger (deck §14). Customer-scoped — a caller may only read their own
/// ledger; the owning customer is <see cref="ResourceId"/>.
/// </summary>
public sealed record GetLedgerQuery(Guid CustomerId) : IQuery<LedgerDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record LedgerDto(
    Guid CustomerId,
    int AssetCount,
    long RecordedReplacementExposureMinor,
    string Currency,
    int CompletenessPercent,
    IReadOnlyList<LedgerRoomDto> Rooms);

public sealed record LedgerRoomDto(string Room, long RoomTotalMinor, IReadOnlyList<LedgerAssetDto> Assets);

public sealed record LedgerAssetDto(
    Guid AssetId, string DisplayName, string Category, string Assurance, long? BestValueMinor, string Status);

internal sealed class GetLedgerHandler(IKadariDbContext db)
    : IQueryHandler<GetLedgerQuery, LedgerDto>
{
    public async Task<LedgerDto> Handle(GetLedgerQuery query, CancellationToken ct)
    {
        var customerId = new CustomerId(query.CustomerId);
        var tenant = await db.TenantOfCustomerAsync(customerId, ct);

        // The person subject backing this customer owns their assets. No subject yet ⇒ an empty ledger
        // (a customer who has not captured anything), not a 404 — existence is settled by the resolver.
        var subjectId = await db.Owners.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (OwnerId?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (subjectId is null)
        {
            return LedgerProjection.From(query.CustomerId, []);
        }

        // The ledger shows what the subject owns — as the primary owner or a household co-owner (deck §31), so
        // a shared item appears in every member's ledger. Only rows on the customer's own tenant count (their insurer's
        // when they are on an insurer's book): defence-in-depth against an asset stamped to some other tenant.
        var assets = await db.Assets.AsNoTracking()
            .Include(a => a.Evidence)
            .Where(a => a.OwnerId == subjectId.Value || a.CoOwners.Any(c => c.MemberId == subjectId.Value))
            .ForTenant(tenant)
            .ToListAsync(ct);

        return LedgerProjection.From(query.CustomerId, assets);
    }
}
