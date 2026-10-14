using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Kadari.Application;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Identity.RefreshToken;
using Kadari.Application.Features.Identity.RequestOtp;
using Kadari.Application.Features.Identity.SetUserStatus;
using Kadari.Application.Features.Identity.VerifyOtp;
using Kadari.Domain.Identity;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Tests.Identity;

/// <summary>
/// End-to-end OTP → JWT flow via MediatR against real Postgres: request stores a challenge and
/// "delivers" a code; verifying it auto-registers a Customer and issues a token; a wrong code is rejected.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpFlowTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private sealed class CapturingDelivery : IOtpDelivery
    {
        public string? LastCode { get; private set; }

        public Task SendAsync(string mobileNumber, string code, CancellationToken ct = default)
        {
            LastCode = code;
            return Task.CompletedTask;
        }
    }

    private readonly CapturingDelivery _delivery = new();

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private ServiceProvider BuildSender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddApplication();   // MediatR + validators + pipeline
        services.AsStaff();   // slice tests run as back-office staff (PAS-002 §2)
        services.AddSingleton<IKadariDbContext>(context);
        services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.AddSingleton<IOtpDelivery>(_delivery);
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

    [Fact]
    public async Task Request_then_verify_auto_registers_and_issues_a_token()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();

        const string mobile = "+27820000001";

        await sender.Send(new RequestOtpCommand(mobile));
        var response = await sender.Send(new VerifyOtpCommand(mobile, _delivery.LastCode!));

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));

        var user = await db.Set<UserAccount>().SingleAsync(u => u.MobileNumber == mobile);
        Assert.Equal(response.UserAccountId, user.Id.Value);
        Assert.NotNull(user.LastLoginAt);
    }

    /// <summary>GL-16: signing out ends the session, so its refresh token no longer works.</summary>
    [Fact]
    public async Task Sign_out_ends_the_session_behind_the_refresh_token()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();
        var mobile = $"+2782{Random.Shared.Next(1000000, 9999999)}";
        await sender.Send(new RequestOtpCommand(mobile));
        var signedIn = await sender.Send(new VerifyOtpCommand(mobile, _delivery.LastCode!));

        await sender.Send(new Application.Features.Identity.SignOut.SignOutCommand(signedIn.RefreshToken));

        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(new RefreshTokenCommand(signedIn.RefreshToken)));
    }

    /// <summary>AS-10: the local spelling and the E.164 spelling are one phone, one challenge, one account.</summary>
    [Fact]
    public async Task A_local_format_number_signs_in_as_its_international_form()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();
        var local = $"082 {Random.Shared.Next(100, 999)} {Random.Shared.Next(1000, 9999)}";
        var international = MobileNumbers.Normalise(local)!;

        await sender.Send(new RequestOtpCommand(local));
        await sender.Send(new VerifyOtpCommand(international, _delivery.LastCode!));

        Assert.True(await db.Set<UserAccount>().AnyAsync(u => u.MobileNumber == international));
    }

    /// <summary>AS-10: launch sends paid codes to South African numbers only.</summary>
    [Fact]
    public async Task A_number_outside_the_allowed_countries_gets_no_code()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new RequestOtpCommand("+447700900123")));
        Assert.Null(_delivery.LastCode);
    }

    /// <summary>
    /// GL-12: suspending an account must lock its owner out — no new sign-in, and the live session ends so its
    /// refresh token stops working at once.
    /// </summary>
    [Fact]
    public async Task A_suspended_account_cannot_sign_in_or_refresh()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();
        var mobile = $"+2782{Random.Shared.Next(1000000, 9999999)}";

        await sender.Send(new RequestOtpCommand(mobile));
        var signedIn = await sender.Send(new VerifyOtpCommand(mobile, _delivery.LastCode!));

        var user = await db.Set<UserAccount>().SingleAsync(u => u.MobileNumber == mobile);
        await sender.Send(new SetUserStatusCommand(user.Id.Value, AccountStatus.Suspended));   // as the portal does

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sender.Send(new RefreshTokenCommand(signedIn.RefreshToken)));

        var session = await db.Set<Session>().AsNoTracking().SingleAsync(s => s.UserAccountId == user.Id);
        Assert.Equal(SessionStatus.Terminated, session.Status);

        // Still suspended: a correct code must not start a new session.
        await sender.Send(new RequestOtpCommand(mobile));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sender.Send(new VerifyOtpCommand(mobile, _delivery.LastCode!)));
        Assert.Equal(1, await db.Set<Session>().CountAsync(s => s.UserAccountId == user.Id));
    }

    /// <summary>
    /// GL-13: after a rotation, the old refresh token must be useless — and presenting it proves the session's
    /// tokens are held by two parties, so the session ends for both.
    /// </summary>
    [Fact]
    public async Task Reusing_a_rotated_refresh_token_ends_the_session()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();
        var mobile = $"+2782{Random.Shared.Next(1000000, 9999999)}";

        await sender.Send(new RequestOtpCommand(mobile));
        var signedIn = await sender.Send(new VerifyOtpCommand(mobile, _delivery.LastCode!));
        var rotated = await sender.Send(new RefreshTokenCommand(signedIn.RefreshToken));   // the thief, say

        await Assert.ThrowsAsync<RefreshTokenReuseException>(() =>
            sender.Send(new RefreshTokenCommand(signedIn.RefreshToken)));                    // the owner, later

        // The termination survived the rejection, so the thief's rotated token is dead too.
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => sender.Send(new RefreshTokenCommand(rotated.RefreshToken)));
        var session = await NewContext().Set<Session>().AsNoTracking()
            .SingleAsync(s => s.UserAccountId == new UserAccountId(signedIn.UserAccountId));
        Assert.Equal(SessionStatus.Terminated, session.Status);
    }

    /// <summary>
    /// GL-15: however many IPs an attacker uses, one number gets at most 5 codes an hour and 10 a day — which caps
    /// guessing at 50 tries per number per day and bounds SMS-pumping spend.
    /// </summary>
    [Theory]
    [InlineData(5, 30)]      // five codes in the last hour
    [InlineData(10, 600)]    // ten codes spread over the day
    public async Task A_number_that_spent_its_code_budget_is_refused(int alreadyIssued, int spreadMinutes)
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();
        var mobile = $"+2782{Random.Shared.Next(1000000, 9999999)}";
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < alreadyIssued; i++)
        {
            var issuedAt = now.AddMinutes(-spreadMinutes * (i + 1.0) / (alreadyIssued + 1));
            db.Set<OtpChallenge>().Add(OtpChallenge.Issue(mobile, "hash", issuedAt.AddMinutes(5)));
        }

        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<TooManyRequestsException>(() => sender.Send(new RequestOtpCommand(mobile)));
    }

    [Fact]
    public async Task Verify_provisions_and_links_a_customer_to_the_account()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();

        const string mobile = "+27820000004";

        await sender.Send(new RequestOtpCommand(mobile));
        await sender.Send(new VerifyOtpCommand(mobile, _delivery.LastCode!));

        // Auto-registration must leave a signed-in customer with the anchor record that every
        // customer-scoped slice — and /me — resolves through (PAS-002 §2).
        var user = await db.Set<UserAccount>().SingleAsync(u => u.MobileNumber == mobile);
        Assert.NotNull(user.CustomerId);

        var customer = await db.Customers.SingleAsync(c => c.Id == user.CustomerId!.Value);
        Assert.Equal(mobile, customer.MobileNumber);
    }

    [Fact]
    public async Task Refresh_issues_new_tokens_and_rotates_the_old_one()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();

        const string mobile = "+27820000003";
        await sender.Send(new RequestOtpCommand(mobile));
        var login = await sender.Send(new VerifyOtpCommand(mobile, _delivery.LastCode!));

        var refreshed = await sender.Send(new RefreshTokenCommand(login.RefreshToken));

        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);   // rotated

        // The old refresh token no longer works.
        await Assert.ThrowsAnyAsync<ForbiddenException>(() =>
            sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }

    [Fact]
    public async Task Wrong_code_is_rejected()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();

        const string mobile = "+27820000002";

        await sender.Send(new RequestOtpCommand(mobile));
        var wrong = _delivery.LastCode == "000000" ? "111111" : "000000";

        await Assert.ThrowsAsync<OtpRejectedException>(() => sender.Send(new VerifyOtpCommand(mobile, wrong)));
    }

    /// <summary>GL-39: the not-found error is logged, so it must not carry the mobile number.</summary>
    [Fact]
    public async Task Verifying_without_a_pending_code_does_not_echo_the_number()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var sender = BuildSender(db).GetRequiredService<ISender>();
        var mobile = $"+2782{Random.Shared.Next(1000000, 9999999)}";

        var error = await Assert.ThrowsAsync<NotFoundException>(() => sender.Send(new VerifyOtpCommand(mobile, "123456")));

        Assert.DoesNotContain(mobile, error.Message);
    }
}
