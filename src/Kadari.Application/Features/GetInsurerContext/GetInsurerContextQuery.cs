using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.GetInsurerContext;

/// <summary>
/// The signed-in insurer's own console context (Kadari §27–28): which tenant they are bound to, and its
/// white-label branding, so the Admin console can theme itself and know which tenant's portfolio to read
/// without the caller passing an id. Identity-derived (resolved from the caller's <see cref="InsurerUser"/>
/// binding), not a customer-owned resource, so it is <see cref="IUnscopedRequest"/>; the endpoint gates it with
/// <c>Permission.InsurerPortfolioView</c>. A caller with no binding (an unbound admin, or a non-insurer) gets a
/// 404 — they have no single "home" tenant.
/// </summary>
public sealed record GetInsurerContextQuery : IQuery<InsurerContextDto>, IUnscopedRequest;

public sealed record InsurerContextDto(Guid TenantId, string Name, string Kind, TenantBrandingDto? Branding);

public sealed record TenantBrandingDto(string PrimaryColor, string AccentColor, string? LogoReference)
{
    public static TenantBrandingDto? From(TenantBranding? b) =>
        b is null ? null : new TenantBrandingDto(b.PrimaryColor, b.AccentColor, b.LogoReference);
}

internal sealed class GetInsurerContextHandler(IKadariDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetInsurerContextQuery, InsurerContextDto>
{
    public async Task<InsurerContextDto> Handle(GetInsurerContextQuery query, CancellationToken ct)
    {
        if (currentUser.AccountId is not { } accountId)
        {
            throw new NotFoundException(nameof(InsurerUser), "current");
        }

        var tenantId = await db.InsurerUsers.AsNoTracking()
            .Where(u => u.UserAccountId == new UserAccountId(accountId))
            .Select(u => (TenantId?)u.TenantId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(InsurerUser), accountId);

        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException(nameof(Tenant), tenantId.Value);

        return new InsurerContextDto(
            tenant.Id.Value,
            tenant.Name,
            tenant.Kind.ToString(),
            TenantBrandingDto.From(tenant.Branding));
    }
}
