using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.GetTenantPortfolio;
using Kadari.Application.Features.GrantInsurerAccess;
using Kadari.Domain.Access;
using Kadari.Domain.Assets;
using Kadari.Domain.Identity;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves insurer auth against real Postgres: provisioning an account as an insurer user gives it the Insurer
/// role and a tenant binding, and the InsurerTenantGuard then confines that user's console reads to their own
/// tenant (403 on another) while an unbound admin reads any.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InsurerAccessTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(KadariDbContext context, ICurrentUser actor)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(actor);
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<(Tenant Insurer, UserAccount Account)> SeedInsurerUserAsync()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var insurer = Tenant.CreateInsurer("Acme Insurance");
        var account = UserAccount.Register("+27820000091", "Ivy", "Insurer", "ZA", Role.SystemAdministrator);

        await using (var db = NewContext())
        {
            db.Tenants.Add(insurer);
            db.Set<UserAccount>().Add(account);
            await db.SaveChangesAsync();
        }

        // Provision as an insurer user (admin actor).
        await using (var db = NewContext())
        {
            await BuildSender(db, Admin()).Send(new GrantInsurerAccessCommand(account.Id.Value, insurer.Id.Value));
        }

        return (insurer, account);
    }

    private static ICurrentUser Admin() => new StubUser(Guid.CreateVersion7(), Role.SystemAdministrator);

    [Fact]
    public async Task Granting_insurer_access_adds_the_role_and_the_tenant_binding()
    {
        var (insurer, account) = await SeedInsurerUserAsync();

        await using var verify = NewContext();
        var reloaded = await verify.Set<UserAccount>().SingleAsync(u => u.Id == account.Id);
        Assert.Contains(Role.Insurer, reloaded.Roles);

        var binding = await verify.InsurerUsers.SingleAsync(u => u.UserAccountId == account.Id);
        Assert.Equal(insurer.Id, binding.TenantId);
    }

    [Fact]
    public async Task An_insurer_reads_their_own_tenant_but_is_forbidden_another()
    {
        var (insurer, account) = await SeedInsurerUserAsync();
        var insurerActor = new StubUser(account.Id.Value, Role.Insurer);

        // Their own tenant: allowed.
        await using (var db = NewContext())
        {
            var portfolio = await BuildSender(db, insurerActor).Send(new GetTenantPortfolioQuery(insurer.Id.Value));
            Assert.Equal(insurer.Id.Value, portfolio.TenantId);
        }

        // A different tenant: forbidden.
        var otherTenant = Tenant.CreateInsurer("Rival Mutual");
        await using (var db = NewContext())
        {
            db.Tenants.Add(otherTenant);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                BuildSender(db, insurerActor).Send(new GetTenantPortfolioQuery(otherTenant.Id.Value)));
        }
    }

    [Fact]
    public async Task An_unbound_admin_reads_any_tenant()
    {
        var (insurer, _) = await SeedInsurerUserAsync();

        await using var db = NewContext();
        // The admin has no InsurerUser binding, so the guard lets them view the insurer's tenant.
        var portfolio = await BuildSender(db, Admin()).Send(new GetTenantPortfolioQuery(insurer.Id.Value));
        Assert.Equal(insurer.Id.Value, portfolio.TenantId);
    }

    [Fact]
    public async Task An_insurer_with_no_tenant_binding_reads_no_tenant()
    {
        var (insurer, _) = await SeedInsurerUserAsync();
        // An Insurer-role account whose binding is missing must not fall through to the admin's "any tenant" (AS-04).
        var unbound = new StubUser(Guid.CreateVersion7(), Role.Insurer);

        await using var db = NewContext();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            BuildSender(db, unbound).Send(new GetTenantPortfolioQuery(insurer.Id.Value)));
    }

    [Fact]
    public async Task Granting_for_an_unknown_tenant_is_not_found()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var account = UserAccount.Register("+27820000092", "Ivy", "Insurer", "ZA", Role.SystemAdministrator);
        await using (var db = NewContext())
        {
            db.Set<UserAccount>().Add(account);
            await db.SaveChangesAsync();
        }

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context, Admin()).Send(new GrantInsurerAccessCommand(account.Id.Value, Guid.CreateVersion7())));
    }
}

file sealed class StubUser(Guid accountId, params Role[] roles) : ICurrentUser
{
    public Guid? AccountId => accountId;
    public IReadOnlyCollection<Role> Roles => roles;
}
