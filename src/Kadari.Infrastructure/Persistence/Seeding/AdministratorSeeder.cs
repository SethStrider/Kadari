using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.Identity.SeedAdministrator;

namespace Kadari.Infrastructure.Persistence.Seeding;

/// <summary>
/// Ensures the platform's first <c>SystemAdministrator</c> exists at start-up. Idempotent, so it is
/// safe on every boot. Like <see cref="CatalogueSeeder"/>, a failure (database not migrated yet) is
/// logged rather than fatal: the API must still start.
/// </summary>
internal sealed class AdministratorSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<AdministratorSeedOptions> options,
    ILogger<AdministratorSeeder> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.MobileNumber) && settings.Administrators.Count == 0)
        {
            logger.LogWarning(
                "No administrator seeded: neither {Section}:MobileNumber nor {Section}:Administrators is "
                + "configured. Nothing can grant SystemAdministrator until one is set.",
                AdministratorSeedOptions.SectionName, AdministratorSeedOptions.SectionName);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // The primary administrator (a single well-known mobile), then any additional team members.
            if (!string.IsNullOrWhiteSpace(settings.MobileNumber))
            {
                await SeedOneAsync(
                    sender,
                    new SeedAdministratorCommand(settings.MobileNumber, settings.Email, settings.Password),
                    cancellationToken);
            }

            foreach (var entry in settings.Administrators)
            {
                if (string.IsNullOrWhiteSpace(entry.MobileNumber))
                {
                    continue;   // an entry with no mobile has no identity to seed against
                }

                await SeedOneAsync(
                    sender,
                    new SeedAdministratorCommand(
                        entry.MobileNumber, entry.Email, entry.Password, entry.FirstName, entry.LastName),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Administrator seeding skipped — the database is not ready.");
        }
    }

    private async Task SeedOneAsync(ISender sender, SeedAdministratorCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);

        // The mobile is a credential — log the account id, not the number.
        logger.LogInformation(
            "Administrator seed complete for account {AccountId}: created={Created}, promoted={Promoted}.",
            result.UserAccountId, result.Created, result.Promoted);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
