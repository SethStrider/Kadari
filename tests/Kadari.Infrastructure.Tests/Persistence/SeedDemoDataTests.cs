using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Development.SeedDemoData;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>The demo cast is created once; a second start-up run adds nobody and nothing.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SeedDemoDataTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Seeding_creates_the_demo_cast_once()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var context = NewContext();
        var sender = BuildSender(context);

        var first = await sender.Send(new SeedDemoDataCommand("Demo!Kadari2026"));
        var second = await sender.Send(new SeedDemoDataCommand("Demo!Kadari2026"));

        Assert.Equal((3, 3, 15, 0, 0, 0),
            (first.StaffCreated, first.CustomersCreated, first.AssetsCreated,
             second.StaffCreated, second.CustomersCreated, second.AssetsCreated));
    }
}
