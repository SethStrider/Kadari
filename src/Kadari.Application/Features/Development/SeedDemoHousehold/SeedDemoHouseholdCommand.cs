using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Owners.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;

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
    private sealed record SampleItem(
        string Name, AssetCategory Category, string Room, long ValueRand, bool HasReceipt = false);

    // A mix that exercises the ledger: four rooms, every evidence level, and values from a lamp to a ring.
    private static readonly SampleItem[] Household =
    [
        new("Samsung 65\" QLED TV", AssetCategory.Electronics, "Lounge", 18_999, HasReceipt: true),
        new("Sonos Arc soundbar", AssetCategory.Electronics, "Lounge", 14_500),
        new("Three-seater leather couch", AssetCategory.Furniture, "Lounge", 22_000),
        new("MacBook Pro 14\"", AssetCategory.Electronics, "Study", 38_999, HasReceipt: true),
        new("Herman Miller office chair", AssetCategory.Furniture, "Study", 16_500),
        new("Diamond engagement ring", AssetCategory.Jewellery, "Main bedroom", 45_000, HasReceipt: true),
        new("Omega Seamaster watch", AssetCategory.Jewellery, "Main bedroom", 72_000),
        new("Framed oil painting", AssetCategory.ArtAndCollectibles, "Main bedroom", 9_500),
        new("Bosch dishwasher", AssetCategory.Electronics, "Kitchen", 11_499, HasReceipt: true),
        new("KitchenAid stand mixer", AssetCategory.Electronics, "Kitchen", 8_999),
    ];

    public async Task<SeedDemoHouseholdResponse> Handle(SeedDemoHouseholdCommand cmd, CancellationToken ct)
    {
        var ownerId = await owners.GetOrCreateForCustomerAsync(new CustomerId(cmd.CustomerId), ct);
        if (await db.Assets.AnyAsync(a => a.OwnerId == ownerId, ct))
        {
            return new SeedDemoHouseholdResponse(0);
        }

        var now = clock.GetUtcNow();
        foreach (var item in Household)
        {
            var asset = Asset.Register(TenantId.Default, ownerId, item.Name, item.Category, now, item.Room);
            var reference = $"demo/{asset.Id.Value:N}";
            var value = new Money(item.ValueRand * 100, "ZAR");

            // The self-asserted floor every captured item has: an image and an AI identification.
            asset.Attach(AssetEvidence.Observed("device", $"{reference}.jpg", now));
            asset.Attach(AssetEvidence.Identified("demo", confidence: 0.94, assertedAt: now));

            // A receipt supports ownership and value; without one the value is only an estimate.
            if (item.HasReceipt)
            {
                asset.Attach(AssetEvidence.OwnershipSupported("receipt", $"{reference}/receipt", now));
                asset.Attach(AssetEvidence.ValueSupported("receipt", value, $"{reference}/receipt", now));
            }
            else
            {
                asset.Attach(AssetEvidence.ValueEstimated("demo", value, now));
            }

            db.Assets.Add(asset);
        }

        await db.SaveChangesAsync(ct);
        return new SeedDemoHouseholdResponse(Household.Length);
    }
}
