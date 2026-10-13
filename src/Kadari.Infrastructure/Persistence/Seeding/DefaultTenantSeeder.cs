using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Kadari.Application.Features.SeedDefaultTenant;

namespace Kadari.Infrastructure.Persistence.Seeding;

/// <summary>
/// Ensures the Kadari platform default tenant exists at start-up (D11) — the partition consumer-direct
/// assets belong to. Idempotent, so it is safe on every boot; like the other seeders, a failure (database
/// not migrated yet) is logged rather than fatal so the API still starts.
/// </summary>
internal sealed class DefaultTenantSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<DefaultTenantSeeder> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await sender.Send(new SeedDefaultTenantCommand(), cancellationToken);
            if (result.Created)
            {
                logger.LogInformation("Default Kadari tenant created.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Default tenant seeding skipped — the database is not ready.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
