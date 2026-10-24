using MediatR;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.Care.DispatchDueCareReminders;

namespace Kadari.Worker.Sweeps;

/// <summary>How often the Care reminder sweep runs, bound from <c>CareSweep</c>.</summary>
public sealed class CareSweepOptions
{
    public const string SectionName = "CareSweep";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Runs the Care reminder sweep on a schedule (CLAUDE.md §6a: scheduled work is a BackgroundService that dispatches a
/// command). Each tick queues due reminders and expires lapsed registration offers; delivery happens through the
/// outbox and <c>DeliverCareReminderConsumer</c>. A failed tick is logged and the next one tries again — the command is
/// idempotent.
/// </summary>
public sealed class CareReminderSweep(
    IServiceScopeFactory scopes, IOptions<CareSweepOptions> options, TimeProvider clock, ILogger<CareReminderSweep> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Care reminder sweep is disabled");
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new DispatchDueCareRemindersCommand(), stoppingToken);
                if (result.Queued + result.Cancelled + result.OffersExpired + result.RegistrationsExpired > 0)
                {
                    logger.LogInformation(
                        "Care sweep: {Queued} reminders queued, {Cancelled} cancelled, {Offers} offers and {Registrations} registrations expired",
                        result.Queued, result.Cancelled, result.OffersExpired, result.RegistrationsExpired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Care reminder sweep failed; the next tick will try again");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
