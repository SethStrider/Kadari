using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.CareRegistry.ApplyRegistryImport;
using Kadari.Application.Features.CareRegistry.PreviewRegistryImport;
using Kadari.Application.Features.CareRegistry.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Care;

/// <summary>Shared plumbing for the Care tests: a context per step, a staff sender, seeded customers with assets, and a
/// registry snapshot builder with one researched rule.</summary>
internal static class CareTestHost
{
    public static readonly DateTimeOffset ResearchedAt = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

    public static KadariDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    public static ISender Sender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<Kadari.Application.Abstractions.Storage.IBlobStorage>(new NullBlobStorage());
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    /// <summary>A customer who owns one active asset.</summary>
    public static async Task<(Guid CustomerId, Guid AssetId)> SeedAssetAsync(
        string connectionString, string mobile, AssetCategory category = AssetCategory.Electronics)
    {
        var customer = Customer.Register("Thandi", "Mokoena", mobile, "ZA", email: $"{mobile.TrimStart('+')}@example.com");
        var owner = Owner.ForCustomer(customer.Id, "Thandi Mokoena");
        var asset = Asset.Register(TenantId.Default, owner.Id, "Bosch washing machine", category, DateTimeOffset.UtcNow);

        await using var db = NewContext(connectionString);
        db.Customers.Add(customer);
        db.Owners.Add(owner);
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return (customer.Id.Value, asset.Id.Value);
    }

    /// <summary>Give the asset a stored receipt (independent ownership evidence), as the proof endpoint would.</summary>
    public static async Task AttachReceiptAsync(string connectionString, Guid assetId)
    {
        await using var db = NewContext(connectionString);
        var asset = await db.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == new AssetId(assetId));
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", $"kadari/receipts/{Guid.NewGuid():N}.doc", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public static CoverageRuleRow BoschRule(string months = "24", string status = "Published", string key = "bosch-major-appliance") =>
        new(2, key, "ZA", "Bosch Home", "MajorAppliance", null, null, "Manufacturer", months, "PurchaseDate", "no", null,
            null, "Two-year manufacturer warranty from the date of purchase.", null, null, "High",
            "Bosch ZA warranty terms", "https://www.bosch-home.com/za/service/warranty", "2026-10-01", status);

    public static RegistrationRuleRow BoschRegistration(string status = "Published") =>
        new(2, "bosch-mybosch", "ZA", "Bosch Home", "MajorAppliance", null, "Guided", "WebPage",
            "https://www.bosch-home.com/za/mybosch", "OwnerName;Email;SerialNumber;PurchaseDate", "28", "12",
            "Register within 28 days to add a year to your warranty.", "Sign in to MyBosch and add the appliance.", "High",
            "MyBosch registration terms", "https://www.bosch-home.com/za/mybosch/terms", "2026-10-01", status);

    public static RoutingRuleRow HisenseRoute(string route = "RetailerCredit", string status = "Published", string retailer = "Makro") =>
        new(2, "hisense-small-appliance-fault", null, "Hisense", "SmallAppliance", null, retailer, "InCover", "Fault", route, "R1",
            "The store you bought it from", "InStore", null, "ProofOfPurchase", "Take it back to the store with the receipt.", "10",
            "High", "Hisense SA warranty page", "https://hisense.co.za/support", "2026-10-01", status);

    public static RegistrySnapshot SnapshotWith(IReadOnlyList<CoverageRuleRow> rules, IReadOnlyList<RegistrationRuleRow> registrations) =>
        Snapshot([.. rules]) with { RegistrationRules = registrations };

    public static RegistrySnapshot Snapshot(params CoverageRuleRow[] rules) => new(
        ResearchedAt, "ZA",
        [
            new BrandRow(2, "Bosch Home", "BSH", "BSH Home Appliances SA", "Major appliances", "OEM network", "Registration", "P0", "High",
                "https://www.bosch-home.com/za/custom"),
            new BrandRow(3, "Hisense", "Hisense", "Hisense SA", "TV, appliances", "Retailer first", null, "P0", "High",
                "https://hisense.co.za/support"),
        ],
        [new RetailerRow(2, "Makro", "Massmart", "Big box", "Appliances", "Self-capture first", "P0", "https://www.makro.co.za/appliances-store")],
        [new RoutingModelRow(2, "R1", "Retailer return / credit", "Low-value", "Tell user which retailer", "Receipt", "Hisense irons")],
        rules);

    /// <summary>Preview, then apply exactly that plan — the migration runner's sequence.</summary>
    public static async Task<RegistryImportResult> ImportAsync(string connectionString, RegistrySnapshot snapshot)
    {
        await using var db = NewContext(connectionString);
        var sender = Sender(db);
        var plan = await sender.Send(new PreviewRegistryImportQuery(snapshot));
        return await sender.Send(new ApplyRegistryImportCommand(snapshot, plan.Fingerprint));
    }
}
