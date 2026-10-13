using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.CreateInsurerTenant;

/// <summary>
/// Onboard an insurer as a white-label tenant (Kadari §27–28) — the missing front door of the insurer
/// console: everything else (assign a cohort, grant a console user, read the portfolio) needs a tenant to
/// already exist. Optionally sets the tenant's branding in the same step. Administrative and cross-tenant, so
/// gated by <c>Permission.AssetTenantAssign</c> at the edge and marked <see cref="IUnscopedRequest"/> here
/// (it is not scoped to a customer-owned resource).
/// </summary>
public sealed record CreateInsurerTenantCommand(string Name, BrandingInput? Branding)
    : ICommand<CreateInsurerTenantResponse>, IUnscopedRequest;

/// <summary>Optional white-label branding supplied at onboarding — hex colours plus an optional logo reference.</summary>
public sealed record BrandingInput(string PrimaryColor, string AccentColor, string? LogoReference);

public sealed record CreateInsurerTenantResponse(Guid TenantId, string Name);

internal sealed class CreateInsurerTenantHandler(IKadariDbContext db)
    : ICommandHandler<CreateInsurerTenantCommand, CreateInsurerTenantResponse>
{
    public async Task<CreateInsurerTenantResponse> Handle(CreateInsurerTenantCommand cmd, CancellationToken ct)
    {
        var tenant = Tenant.CreateInsurer(cmd.Name);

        if (cmd.Branding is { } b)
        {
            tenant.Rebrand(new TenantBranding(b.PrimaryColor, b.AccentColor, b.LogoReference));
        }

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        return new CreateInsurerTenantResponse(tenant.Id.Value, tenant.Name);
    }
}
