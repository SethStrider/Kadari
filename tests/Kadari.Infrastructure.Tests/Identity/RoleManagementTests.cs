using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Identity.AssignRole;
using Kadari.Application.Features.Identity.RemoveRole;
using Kadari.Application.Features.Identity.SeedAdministrator;
using Kadari.Domain.Abstractions;
using Kadari.Domain.Access;
using Kadari.Domain.Identity;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Tests.Identity;

/// <summary>
/// Role administration against real Postgres: grants are additive (a Customer can also be an
/// ambassador), revokes cannot strand an account or let an administrator lock themselves out, and the
/// administrator seed is idempotent — including when the account already exists from an OTP sign-in.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RoleManagementTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private sealed class FakeCurrentUser(Guid? accountId, params Role[] roles) : ICurrentUser
    {
        public Guid? AccountId { get; } = accountId;

        public IReadOnlyCollection<Role> Roles { get; } = roles;
    }

    private const string AdminMobile = "+27820000100";

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(KadariDbContext context, ICurrentUser? currentUser = null)
    {
        var services = new ServiceCollection();
        services.AddApplication();   // MediatR + validators + pipeline
        services.AsStaff();   // slice tests run as back-office staff (PAS-002 §2)
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<Application.Abstractions.Messaging.IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();   // SeedAdministrator seeds a password
        services.AddSingleton(currentUser ?? new FakeCurrentUser(Guid.NewGuid(), Role.SystemAdministrator));
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private static async Task<UserAccount> AddCustomerAsync(KadariDbContext db, string mobile)
    {
        var user = UserAccount.Register(mobile, "Jane", "Doe", "ZA");
        db.Set<UserAccount>().Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task AssignRole_adds_the_role_without_replacing_the_existing_ones()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        var user = await AddCustomerAsync(db, "+27820000101");

        var response = await sender.Send(new AssignRoleCommand(user.Id.Value, Role.Valuer));

        Assert.Equal(["Customer", "Valuer"], response.Roles);
    }

    [Fact]
    public async Task AssignRole_persists_a_customer_who_is_also_a_valuer()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        var user = await AddCustomerAsync(db, "+27820000102");

        await sender.Send(new AssignRoleCommand(user.Id.Value, Role.Valuer));

        await using var reread = NewContext();
        var stored = await reread.Set<UserAccount>().SingleAsync(u => u.Id == user.Id);
        // The union of both roles' grants is what the account can actually do (PCS-004 §14).
        Assert.True(RolePermissions.HasAny(stored.Roles, Permission.AssetCapture));      // from Customer
        Assert.True(RolePermissions.HasAny(stored.Roles, Permission.AssetValuate));      // from Valuer
    }

    [Fact]
    public async Task AssignRole_is_idempotent_when_the_role_is_already_held()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        var user = await AddCustomerAsync(db, "+27820000103");

        await sender.Send(new AssignRoleCommand(user.Id.Value, Role.Valuer));
        var response = await sender.Send(new AssignRoleCommand(user.Id.Value, Role.Valuer));

        Assert.Equal(2, response.Roles.Count);
    }

    [Fact]
    public async Task AssignRole_rejects_the_anonymous_role()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        var user = await AddCustomerAsync(db, "+27820000104");

        await Assert.ThrowsAsync<ValidationException>(() =>
            sender.Send(new AssignRoleCommand(user.Id.Value, Role.PublicVisitor)));
    }

    [Fact]
    public async Task AssignRole_rejects_the_insurer_role_which_needs_a_tenant_binding()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        var user = await AddCustomerAsync(db, "+27820000109");

        await Assert.ThrowsAsync<ValidationException>(() =>
            sender.Send(new AssignRoleCommand(user.Id.Value, Role.Insurer)));
    }

    [Fact]
    public async Task AssignRole_when_the_account_is_unknown_throws_not_found()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sender.Send(new AssignRoleCommand(Guid.CreateVersion7(), Role.Valuer)));
    }

    [Fact]
    public async Task RemoveRole_revokes_the_role_and_keeps_the_rest()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        var user = await AddCustomerAsync(db, "+27820000105");
        await sender.Send(new AssignRoleCommand(user.Id.Value, Role.Valuer));

        var response = await sender.Send(new RemoveRoleCommand(user.Id.Value, Role.Valuer));

        Assert.Equal(["Customer"], response.Roles);
    }

    [Fact]
    public async Task RemoveRole_cannot_strand_an_account_with_no_role()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        var user = await AddCustomerAsync(db, "+27820000106");

        await Assert.ThrowsAsync<DomainException>(() =>
            sender.Send(new RemoveRoleCommand(user.Id.Value, Role.Customer)));
    }

    [Fact]
    public async Task RemoveRole_refuses_an_administrator_revoking_their_own_administration()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var user = await AddCustomerAsync(db, "+27820000107");
        var sender = BuildSender(db, new FakeCurrentUser(user.Id.Value, Role.SystemAdministrator));
        await sender.Send(new AssignRoleCommand(user.Id.Value, Role.SystemAdministrator));

        await Assert.ThrowsAsync<ConflictException>(() =>
            sender.Send(new RemoveRoleCommand(user.Id.Value, Role.SystemAdministrator)));
    }

    [Fact]
    public async Task RemoveRole_allows_one_administrator_to_demote_another()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var target = await AddCustomerAsync(db, "+27820000108");
        var sender = BuildSender(db, new FakeCurrentUser(Guid.CreateVersion7(), Role.SystemAdministrator));
        await sender.Send(new AssignRoleCommand(target.Id.Value, Role.SystemAdministrator));

        var response = await sender.Send(new RemoveRoleCommand(target.Id.Value, Role.SystemAdministrator));

        Assert.Equal(["Customer"], response.Roles);
    }

    [Fact]
    public async Task SeedAdministrator_creates_the_first_system_administrator()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);

        var response = await sender.Send(new SeedAdministratorCommand(AdminMobile));

        Assert.True(response.Created);
        var admin = await db.Set<UserAccount>().SingleAsync(u => u.MobileNumber == AdminMobile);
        Assert.Equal([Role.SystemAdministrator], admin.Roles);
        Assert.Equal(AdministratorSeed.Email, admin.Email);
        Assert.True(RolePermissions.HasAny(admin.Roles, Permission.AdminManageRoles));
        Assert.True(RolePermissions.HasAny(admin.Roles, Permission.CustomerErase));
    }

    [Fact]
    public async Task SeedAdministrator_run_twice_creates_only_one_account()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);

        var first = await sender.Send(new SeedAdministratorCommand(AdminMobile));
        var second = await sender.Send(new SeedAdministratorCommand(AdminMobile));

        Assert.Equal(first.UserAccountId, second.UserAccountId);
        Assert.False(second.Created);
        Assert.False(second.Promoted);
    }

    [Fact]
    public async Task SeedAdministrator_promotes_an_account_that_already_signed_in()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db);
        // OTP auto-registers an unknown mobile as a Customer, so the seed can find one already there.
        var existing = await AddCustomerAsync(db, AdminMobile);

        var response = await sender.Send(new SeedAdministratorCommand(AdminMobile));

        Assert.False(response.Created);
        Assert.True(response.Promoted);
        Assert.Equal(existing.Id.Value, response.UserAccountId);

        await using var reread = NewContext();
        var stored = await reread.Set<UserAccount>().SingleAsync(u => u.MobileNumber == AdminMobile);
        Assert.Equal([Role.Customer, Role.SystemAdministrator], stored.Roles);
    }
}
