using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.SetTenantBranding;

/// <summary>
/// Set (or replace) a tenant's white-label branding for the insurer console (Kadari §27–28). Administrative
/// and cross-tenant, so gated by <c>Permission.AssetTenantAssign</c> at the edge and marked
/// <see cref="IUnscopedRequest"/> here (not a customer-owned resource). 404 when the tenant does not exist.
/// </summary>
public sealed record SetTenantBrandingCommand(Guid TenantId, string PrimaryColor, string AccentColor, string? LogoReference)
    : ICommand<SetTenantBrandingResponse>, IUnscopedRequest;

public sealed record SetTenantBrandingResponse(Guid TenantId);

internal sealed class SetTenantBrandingHandler(IKadariDbContext db)
    : ICommandHandler<SetTenantBrandingCommand, SetTenantBrandingResponse>
{
    public async Task<SetTenantBrandingResponse> Handle(SetTenantBrandingCommand cmd, CancellationToken ct)
    {
        var tenantId = new TenantId(cmd.TenantId);

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException(nameof(Tenant), cmd.TenantId);

        tenant.Rebrand(new TenantBranding(cmd.PrimaryColor, cmd.AccentColor, cmd.LogoReference));
        await db.SaveChangesAsync(ct);

        return new SetTenantBrandingResponse(cmd.TenantId);
    }
}
