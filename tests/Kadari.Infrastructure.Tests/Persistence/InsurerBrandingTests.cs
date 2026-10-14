using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AssignTenant;
using Kadari.Application.Features.CreateInsurerTenant;
using Kadari.Application.Features.GetCustomerBranding;
using Kadari.Application.Features.GetInsurerContext;
using Kadari.Application.Features.GrantInsurerAccess;
using Kadari.Application.Features.SetTenantBranding;
using Kadari.Domain.Access;
using Kadari.Domain.Customers;
using Kadari.Domain.Identity;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the insurer white-label flow against real Postgres: an admin onboards an insurer tenant (optionally
/// with branding) and grants a user console access; that user then reads its own console context — tenant
/// identity + branding — which is what the Admin portal themes itself with. A caller with no insurer binding
/// has no context (404).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InsurerBrandingTests(PostgresFixture fixture) : InfraTest(fixture)
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

    private static ICurrentUser Admin() => new StubUser(Guid.CreateVersion7(), Role.SystemAdministrator);

    private async Task<(Guid TenantId, UserAccount Account)> OnboardInsurerAsync(string mobile, BrandingInput? branding)
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        Guid tenantId;
        await using (var db = NewContext())
        {
            tenantId = (await BuildSender(db, Admin())
                .Send(new CreateInsurerTenantCommand("Acme Insurance", branding))).TenantId;
        }

        var account = UserAccount.Register(mobile, "Ivy", "Insurer", "ZA", Role.SystemAdministrator);
        await using (var db = NewContext())
        {
            db.Set<UserAccount>().Add(account);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            await BuildSender(db, Admin()).Send(new GrantInsurerAccessCommand(account.Id.Value, tenantId));
        }

        return (tenantId, account);
    }

    [Fact]
    public async Task Onboarding_with_branding_then_the_bound_insurer_reads_its_themed_context()
    {
        var (tenantId, account) = await OnboardInsurerAsync(
            "+27820000201", new BrandingInput("#112233", "#00dcc2", "brand/acme-logo.png"));

        await using var db = NewContext();
        var context = await BuildSender(db, new StubUser(account.Id.Value, Role.Insurer))
            .Send(new GetInsurerContextQuery());

        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal("Acme Insurance", context.Name);
        Assert.Equal("Insurer", context.Kind);
        Assert.NotNull(context.Branding);
        Assert.Equal("#112233", context.Branding!.PrimaryColor);
        Assert.Equal("#00DCC2", context.Branding.AccentColor);   // normalised to upper-case
        Assert.Equal("brand/acme-logo.png", context.Branding.LogoReference);
    }

    [Fact]
    public async Task Setting_branding_later_is_reflected_in_the_context()
    {
        var (tenantId, account) = await OnboardInsurerAsync("+27820000202", branding: null);
        var insurerActor = new StubUser(account.Id.Value, Role.Insurer);

        await using (var db = NewContext())
        {
            var before = await BuildSender(db, insurerActor).Send(new GetInsurerContextQuery());
            Assert.Null(before.Branding);
        }

        await using (var db = NewContext())
        {
            await BuildSender(db, Admin())
                .Send(new SetTenantBrandingCommand(tenantId, "#FF0000", "#00FF00", null));
        }

        await using (var db = NewContext())
        {
            var after = await BuildSender(db, insurerActor).Send(new GetInsurerContextQuery());
            Assert.NotNull(after.Branding);
            Assert.Equal("#FF0000", after.Branding!.PrimaryColor);
            Assert.Null(after.Branding.LogoReference);
        }
    }

    [Fact]
    public async Task A_customer_in_a_branded_insurer_cohort_reads_that_insurers_branding_for_their_app()
    {
        var (tenantId, _) = await OnboardInsurerAsync(
            "+27820000203", new BrandingInput("#7C3367", "#C0559B", null));
        var customer = Customer.Register("Thandi", "Nkosi", $"+2782{Random.Shared.Next(1000000, 9999999)}", "ZA");
        await using (var db = NewContext())
        {
            db.Set<Customer>().Add(customer);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            await BuildSender(db, Admin()).Send(new AssignCustomerToTenantCommand(customer.Id.Value, tenantId));
        }

        await using var context = NewContext();
        var branding = await BuildSender(context, Admin())
            .Send(new GetCustomerBrandingQuery(customer.Id.Value));

        Assert.Equal(("Acme Insurance", "#7C3367"), (branding.InsurerName, branding.Branding?.PrimaryColor));
    }

    [Fact]
    public async Task A_caller_with_no_insurer_binding_has_no_context()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context, Admin()).Send(new GetInsurerContextQuery()));
    }
}

file sealed class StubUser(Guid accountId, params Role[] roles) : ICurrentUser
{
    public Guid? AccountId => accountId;
    public IReadOnlyCollection<Role> Roles => roles;
}
