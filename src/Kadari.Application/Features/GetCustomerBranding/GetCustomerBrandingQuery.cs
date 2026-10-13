using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.GetInsurerContext;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.GetCustomerBranding;

/// <summary>
/// The white-label skin for a customer's own KADARI app: the insurer whose cohort they belong to
/// (<see cref="TenantMembership"/>) and that tenant's branding, which the consumer client lays over the
/// KADARI tokens. A consumer-direct customer (no membership, or an unbranded tenant) gets nulls — the
/// default KADARI look stands. Customer-scoped: a caller may only read their own.
/// </summary>
public sealed record GetCustomerBrandingQuery(Guid CustomerId)
    : IQuery<CustomerBrandingDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record CustomerBrandingDto(string? InsurerName, TenantBrandingDto? Branding);

internal sealed class GetCustomerBrandingHandler(IKadariDbContext db)
    : IQueryHandler<GetCustomerBrandingQuery, CustomerBrandingDto>
{
    public async Task<CustomerBrandingDto> Handle(GetCustomerBrandingQuery query, CancellationToken ct)
    {
        var customerId = new CustomerId(query.CustomerId);

        var tenant = await db.TenantMemberships.AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .Join(db.Tenants.AsNoTracking(), m => m.TenantId, t => t.Id, (_, t) => t)
            .FirstOrDefaultAsync(ct);

        return tenant is null
            ? new CustomerBrandingDto(null, null)
            : new CustomerBrandingDto(tenant.Name, TenantBrandingDto.From(tenant.Branding));
    }
}
