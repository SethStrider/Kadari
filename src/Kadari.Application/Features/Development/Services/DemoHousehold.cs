using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Features.Development.Services;

/// <summary>One item of a demo household. A receipt supports ownership and value; without one the value is an estimate.</summary>
internal sealed record DemoItem(string Name, AssetCategory Category, string Room, long ValueRand, bool HasReceipt = false);

/// <summary>Development only: registers sample assets for an owner, with the evidence a real capture would leave.</summary>
internal static class DemoHousehold
{
    /// <summary>Ten items over four rooms, every evidence level, from a mixer to a watch (R258k).</summary>
    public static readonly DemoItem[] Family =
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

    /// <summary>A smaller flat: five items over three rooms, two with receipts.</summary>
    public static readonly DemoItem[] Flat =
    [
        new("LG 55\" OLED TV", AssetCategory.Electronics, "Living room", 24_999, HasReceipt: true),
        new("PlayStation 5", AssetCategory.Electronics, "Living room", 11_999),
        new("Trek mountain bike", AssetCategory.Other, "Garage", 32_000, HasReceipt: true),
        new("Weber gas braai", AssetCategory.Other, "Garage", 9_800),
        new("Gold chain necklace", AssetCategory.Jewellery, "Bedroom", 15_500),
    ];

    /// <summary>Adds the items' assets to the context (not saved). Returns how many were added.</summary>
    public static int Register(
        IKadariDbContext db, OwnerId ownerId, TenantId tenantId, IEnumerable<DemoItem> items, DateTimeOffset now)
    {
        var count = 0;
        foreach (var item in items)
        {
            var asset = Asset.Register(tenantId, ownerId, item.Name, item.Category, now, item.Room);
            var reference = $"demo/{asset.Id.Value:N}";
            var value = new Money(item.ValueRand * 100, "ZAR");

            // The self-asserted floor every captured item has: an image and an AI identification.
            asset.Attach(AssetEvidence.Observed("device", $"{reference}.jpg", now));
            asset.Attach(AssetEvidence.Identified("demo", confidence: 0.94, assertedAt: now));

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
            count++;
        }

        return count;
    }
}
