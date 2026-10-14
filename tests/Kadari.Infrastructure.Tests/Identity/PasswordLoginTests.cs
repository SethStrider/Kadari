using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Kadari.Application;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Identity.PasswordLogin;
using Kadari.Domain.Access;
using Kadari.Domain.Identity;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Tests.Identity;

/// <summary>
/// Staff email+password sign-in via MediatR against real Postgres: a staff account with a password gets
/// a token; a wrong password is rejected and counted; and non-staff / suspended / unknown-email accounts
/// are refused with the same generic failure (no account enumeration).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PasswordLoginTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private const string Password = "Sup3r!Secret";

    private readonly Pbkdf2PasswordHasher _hasher = new();

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private ServiceProvider BuildSender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddApplication();   // MediatR + validators + pipeline
        services.AsStaff();   // the pipeline needs a current user; login itself is unscoped
        services.AddSingleton<IKadariDbContext>(context);
        services.AddSingleton<IPasswordHasher>(_hasher);
        services.AddScoped<Application.Abstractions.Messaging.IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton(Options.Create(new JwtOptions
        {
            SigningKey = "trustli-dev-signing-key-at-least-32-bytes-long!!",
        }));
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider();
    }

    private async Task SeedAsync(
        KadariDbContext db, string mobile, string email, Role role,
        bool withPassword = true, AccountStatus status = AccountStatus.Active)
    {
        var user = UserAccount.Register(mobile, "Ada", "Admin", "ZA", role, email);
        if (withPassword)
        {
            user.SetPassword(_hasher.Hash(Password));
        }

        if (status == AccountStatus.Suspended)
        {
            user.Suspend();
        }

        db.Set<UserAccount>().Add(user);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Staff_with_a_password_can_sign_in()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        await SeedAsync(db, "+27820000101", "boss@trustli.co.za", Role.SystemAdministrator);
        var sender = BuildSender(db).GetRequiredService<ISender>();

        var response = await sender.Send(new PasswordLoginCommand("boss@trustli.co.za", Password));

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));

        var user = await db.Set<UserAccount>().SingleAsync(u => u.Email == "boss@trustli.co.za");
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_and_counts_the_attempt()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        await SeedAsync(db, "+27820000102", "boss@trustli.co.za", Role.SystemAdministrator);
        var sender = BuildSender(db).GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            sender.Send(new PasswordLoginCommand("boss@trustli.co.za", "wrong")));

        // The brute-force increment survives the rejection (IPersistableRejection).
        await using var verify = NewContext();
        var user = await verify.Set<UserAccount>().SingleAsync(u => u.Email == "boss@trustli.co.za");
        Assert.Equal(1, user.FailedLoginAttempts);
    }

    [Fact]
    public async Task Non_staff_account_is_refused_even_with_the_correct_password()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        await SeedAsync(db, "+27820000103", "customer@trustli.co.za", Role.Customer);
        var sender = BuildSender(db).GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            sender.Send(new PasswordLoginCommand("customer@trustli.co.za", Password)));
    }

    [Fact]
    public async Task Suspended_staff_account_is_refused()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        await SeedAsync(db, "+27820000104", "gone@trustli.co.za", Role.SystemAdministrator,
            status: AccountStatus.Suspended);
        var sender = BuildSender(db).GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            sender.Send(new PasswordLoginCommand("gone@trustli.co.za", Password)));
    }

    [Fact]
    public async Task Unknown_email_is_refused()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            sender.Send(new PasswordLoginCommand("nobody@trustli.co.za", Password)));
    }
}
