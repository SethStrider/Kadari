using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.Development.SeedDemoData;

namespace Kadari.Infrastructure.Persistence.Seeding;

/// <summary>Switches the demo cast on (<c>DemoSeed:Enabled</c>) and sets the staff portal password.</summary>
public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public bool Enabled { get; init; }
    public string StaffPassword { get; init; } = string.Empty;
}

/// <summary>
/// Development only: seeds the demo cast (<see cref="SeedDemoDataCommand"/>) at start-up. Registered only when the host
/// runs in Development, and does nothing unless <c>DemoSeed:Enabled</c> is true. Like the other boot seeders, a failure
/// is logged rather than fatal.
/// </summary>
internal sealed class DemoDataSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<DemoSeedOptions> options,
    ILogger<DemoDataSeeder> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.StaffPassword))
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new SeedDemoDataCommand(settings.StaffPassword), cancellationToken);

            logger.LogInformation(
                "Demo seed complete: staff={Staff}, customers={Customers}, assets={Assets} created.",
                result.StaffCreated, result.CustomersCreated, result.AssetsCreated);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Demo seeding skipped.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
