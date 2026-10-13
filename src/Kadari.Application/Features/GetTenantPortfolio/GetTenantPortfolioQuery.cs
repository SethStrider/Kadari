using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Tenancy;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.GetTenantPortfolio;

/// <summary>
/// Read an insurer tenant's whole-cohort portfolio (deck §27–28). Deliberately <b>not</b> customer-scoped:
/// this is cross-customer oversight for a tenant, gated at the edge by <c>Permission.InsurerPortfolioView</c>
/// rather than by ownership — so it is an <see cref="IUnscopedRequest"/>. (Insurer-principal self-restriction
/// — confining a caller to their own tenant — is the next layer, via the <c>ICurrentTenant</c> seam; today the
/// permission is the constraint.)
/// </summary>
public sealed record GetTenantPortfolioQuery(Guid TenantId) : IQuery<TenantPortfolioDto>, IUnscopedRequest;

internal sealed class GetTenantPortfolioHandler(IKadariDbContext db, InsurerTenantGuard guard)
    : IQueryHandler<GetTenantPortfolioQuery, TenantPortfolioDto>
{
    public async Task<TenantPortfolioDto> Handle(GetTenantPortfolioQuery query, CancellationToken ct)
    {
        await guard.EnsureCanViewAsync(query.TenantId, ct);

        var tenantId = new TenantId(query.TenantId);

        // Settle existence: an unknown tenant is a 404, distinct from a real tenant that simply holds no assets.
        var tenantExists = await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
        {
            throw new NotFoundException(nameof(Tenant), query.TenantId);
        }

        // Streamed and untracked (GL-37): an insurer's whole book never sits in memory at once — each asset is folded
        // into the running totals and dropped.
        var portfolio = new TenantPortfolioAccumulator(query.TenantId);
        await foreach (var asset in db.Assets.AsNoTracking()
                           .Include(a => a.Evidence)
                           .Where(a => a.TenantId == tenantId)
                           .AsAsyncEnumerable()
                           .WithCancellation(ct))
        {
            portfolio.Add(asset);
        }

        return portfolio.Build();
    }
}
