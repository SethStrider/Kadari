using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.SeedDefaultTenant;

/// <summary>
/// Ensures the well-known platform default tenant exists (D11) — the partition every consumer-direct
/// customer's assets are stamped with. Idempotent: safe on every boot. Unscoped: this is infrastructure
/// seeding with no authenticated caller and no customer-owned resource to authorize against.
/// </summary>
public sealed record SeedDefaultTenantCommand : ICommand<SeedDefaultTenantResponse>, IUnscopedRequest;

/// <param name="Created">The default tenant row was created by this call (false when it already existed).</param>
public sealed record SeedDefaultTenantResponse(bool Created);

internal sealed class SeedDefaultTenantHandler(IKadariDbContext db)
    : ICommandHandler<SeedDefaultTenantCommand, SeedDefaultTenantResponse>
{
    public async Task<SeedDefaultTenantResponse> Handle(SeedDefaultTenantCommand command, CancellationToken ct)
    {
        if (await db.Tenants.AnyAsync(t => t.Id == TenantId.Default, ct))
        {
            return new SeedDefaultTenantResponse(false);
        }

        db.Tenants.Add(Tenant.CreatePlatformDefault());
        await db.SaveChangesAsync(ct);
        return new SeedDefaultTenantResponse(true);
    }
}
