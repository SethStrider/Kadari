using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Identity.RequestOtp;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Tests.Identity;

/// <summary>
/// AS-23 against real Postgres: a transient failure makes the retrying strategy re-run the whole OTP request,
/// yet the customer gets one text with the code that was actually stored, sent only after the commit.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpRetryTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private sealed class RetryOnTimeout(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10))
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }

    /// <summary>The connection drops as the first commit goes out: everything in the command, including an
    /// in-transaction send, has already happened once.</summary>
    private sealed class DropFirstCommit : DbTransactionInterceptor
    {
        private int _commits;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            System.Data.Common.DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
            => ++_commits == 1
                ? throw new TimeoutException("simulated transient failure at commit")
                : base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    private sealed class CountingDelivery : IOtpDelivery
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(string mobileNumber, string code, CancellationToken ct = default)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_retried_otp_request_sends_one_code_after_the_commit()
    {
        await using (var setup = new KadariDbContext(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString).Options))
        {
            await setup.Database.MigrateAsync();
        }

        await using var db = new KadariDbContext(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.ExecutionStrategy(d => new RetryOnTimeout(d)))
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System), new DropFirstCommit())
            .Options);

        var delivery = new CountingDelivery();
        var services = new ServiceCollection();
        services.AddSingleton<IKadariDbContext>(db);
        services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(db));   // the real retrying transaction
        services.AddApplication();
        services.AsStaff();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.AddSingleton<IOtpDelivery>(delivery);
        services.AddSingleton(TimeProvider.System);
        await using var provider = services.BuildServiceProvider();

        var mobile = $"+2782{Random.Shared.Next(1000000, 9999999)}";
        await provider.GetRequiredService<ISender>().Send(new RequestOtpCommand(mobile));

        var code = Assert.Single(delivery.Codes);
        var codes = provider.GetRequiredService<IOtpCodeGenerator>();
        var stored = await db.OtpChallenges.AsNoTracking().SingleAsync(c => c.MobileNumber == mobile);
        Assert.Equal(stored.CodeHash, codes.Hash(code));   // the text carries the code that was committed
    }
}
