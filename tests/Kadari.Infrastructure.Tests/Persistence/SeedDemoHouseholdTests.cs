using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Development.SeedDemoHousehold;
using Kadari.Application.Features.GetLedger;
using Kadari.Domain.Customers;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>The demo seeder fills an empty ledger once and never adds a second household on a re-run.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SeedDemoHouseholdTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Seeding_fills_an_empty_ledger_once()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Demo", "Owner", "+27825550199", "ZA");
        await using (var seed = NewContext())
        {
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
        }

        await using var context = NewContext();
        var sender = BuildSender(context);

        var first = await sender.Send(new SeedDemoHouseholdCommand(customer.Id.Value));
        var second = await sender.Send(new SeedDemoHouseholdCommand(customer.Id.Value));
        var ledger = await sender.Send(new GetLedgerQuery(customer.Id.Value));

        Assert.Equal((10, 0, 10, 4), (first.AssetsCreated, second.AssetsCreated, ledger.AssetCount, ledger.Rooms.Count));
    }
}
