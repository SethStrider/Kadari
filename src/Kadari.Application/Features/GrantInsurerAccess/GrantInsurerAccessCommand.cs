using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Access;
using Kadari.Domain.Assets;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.GrantInsurerAccess;

/// <summary>
/// Provision a user account as an insurer-console user for a tenant (Kadari §27–28) — the identity half of
/// insurer auth. Adds the <see cref="Role.Insurer"/> role (so the account carries
/// <c>Permission.InsurerPortfolioView</c> and signs in via the staff password path) and binds it to the tenant
/// via an <see cref="InsurerUser"/> mapping (so <c>InsurerTenantGuard</c> confines its reads to that tenant).
/// Administrative and cross-tenant, so gated by <c>Permission.AssetTenantAssign</c> and
/// <see cref="IUnscopedRequest"/>. Idempotent: re-granting re-points the tenant and re-adds the role.
/// </summary>
public sealed record GrantInsurerAccessCommand(Guid UserAccountId, Guid TenantId)
    : ICommand<GrantInsurerAccessResponse>, IUnscopedRequest;

public sealed record GrantInsurerAccessResponse(Guid UserAccountId, Guid TenantId);

internal sealed class GrantInsurerAccessHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<GrantInsurerAccessCommand, GrantInsurerAccessResponse>
{
    public async Task<GrantInsurerAccessResponse> Handle(GrantInsurerAccessCommand cmd, CancellationToken ct)
    {
        var accountId = new UserAccountId(cmd.UserAccountId);
        var tenantId = new TenantId(cmd.TenantId);
        var now = clock.GetUtcNow();

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, ct))
        {
            throw new NotFoundException(nameof(Tenant), cmd.TenantId);
        }

        var user = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.Id == accountId, ct)
            ?? throw new NotFoundException(nameof(UserAccount), cmd.UserAccountId);

        user.AddRole(Role.Insurer);   // idempotent on the aggregate

        var binding = await db.InsurerUsers.FirstOrDefaultAsync(u => u.UserAccountId == accountId, ct);
        if (binding is null)
        {
            db.InsurerUsers.Add(InsurerUser.Grant(accountId, tenantId, now));
        }
        else
        {
            binding.Reassign(tenantId, now);
        }

        await db.SaveChangesAsync(ct);

        return new GrantInsurerAccessResponse(cmd.UserAccountId, cmd.TenantId);
    }
}
