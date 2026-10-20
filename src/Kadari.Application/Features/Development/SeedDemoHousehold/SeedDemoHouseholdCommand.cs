using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Development.Services;
using Kadari.Application.Features.Owners.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Development.SeedDemoHousehold;

/// <summary>
/// Development only: fills the signed-in customer's own ledger with a sample household so a demo does not start
/// empty. Mapped only when the host runs in Development, and kept out of the published API contract. Scoped like
/// any customer write: a caller may only seed their own ledger. Idempotent: a ledger that already holds assets is
/// left as it is.
/// </summary>
public sealed record SeedDemoHouseholdCommand(Guid CustomerId)
    : ICommand<SeedDemoHouseholdResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record SeedDemoHouseholdResponse(int AssetsCreated);

internal sealed class SeedDemoHouseholdHandler(IKadariDbContext db, OwnerRegistrar owners, TimeProvider clock)
    : ICommandHandler<SeedDemoHouseholdCommand, SeedDemoHouseholdResponse>
{
    public async Task<SeedDemoHouseholdResponse> Handle(SeedDemoHouseholdCommand cmd, CancellationToken ct)
    {
        var ownerId = await owners.GetOrCreateForCustomerAsync(new CustomerId(cmd.CustomerId), ct);
        if (await db.Assets.AnyAsync(a => a.OwnerId == ownerId, ct))
        {
            return new SeedDemoHouseholdResponse(0);
        }

        var created = DemoHousehold.Register(db, ownerId, TenantId.Default, DemoHousehold.Family, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new SeedDemoHouseholdResponse(created);
    }
}
