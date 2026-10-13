using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.PurgeEphemeralFrames;

namespace Kadari.Infrastructure.Tasks;

/// <summary>
/// The Kadari ephemeral-frame retention worker (D8/POPIA): a hosted service that wakes on an interval and
/// dispatches <see cref="PurgeEphemeralFramesCommand"/>, purging scan frames past their retention window while
/// keeping canonical evidence images. A plain <see cref="PeriodicTimer"/> loop, worker-only. A sweep failure
/// is logged and retried next tick; it never brings the host down.
/// </summary>
public sealed class AssetEvidenceRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<AssetEvidenceRetentionOptions> options,
    ILogger<AssetEvidenceRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.SweepIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await sender.Send(new PurgeEphemeralFramesCommand(), ct);
            if (result.Frames > 0)
            {
                logger.LogInformation(
                    "Purged {Frames} ephemeral scan frame(s) across {Assets} asset(s).", result.Frames, result.Assets);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down — not an error.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kadari ephemeral-frame retention sweep failed; will retry next tick.");
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
