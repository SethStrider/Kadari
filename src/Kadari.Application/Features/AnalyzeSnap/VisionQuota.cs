using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>Reads and records the per-customer and platform-wide daily vision-call counts (AS-09).</summary>
internal sealed class VisionQuota(IKadariDbContext db, IOptions<VisionQuotaOptions> options, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>Whether one more metered call today stays within both caps.</summary>
    public async Task<bool> HasRoomAsync(CustomerId customerId, CancellationToken ct)
    {
        var limits = options.Value;
        var today = Today;

        if (limits.DailyCallsPerCustomer > 0)
        {
            var mine = await db.VisionUsages.AsNoTracking()
                .Where(u => u.CustomerId == customerId && u.Day == today)
                .Select(u => u.Calls)
                .FirstOrDefaultAsync(ct);
            if (mine >= limits.DailyCallsPerCustomer)
            {
                return false;
            }
        }

        if (limits.DailyCallsTotal > 0)
        {
            var total = await db.VisionUsages.AsNoTracking().Where(u => u.Day == today).SumAsync(u => u.Calls, ct);
            if (total >= limits.DailyCallsTotal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Counts one metered call for today; saved with the capture's transaction.</summary>
    public async Task RecordCallAsync(CustomerId customerId, CancellationToken ct)
    {
        var today = Today;
        var usage = await db.VisionUsages.FirstOrDefaultAsync(u => u.CustomerId == customerId && u.Day == today, ct);
        if (usage is null)
        {
            usage = VisionUsage.Start(customerId, today);
            db.VisionUsages.Add(usage);
        }

        usage.RecordCall();
    }
}
