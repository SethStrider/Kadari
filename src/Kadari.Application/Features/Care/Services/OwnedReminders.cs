using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

/// <summary>Finds a reminder on an asset in the customer's own ledger, or 404s — the reminder slices' ownership check.</summary>
internal sealed class OwnedReminders(IKadariDbContext db, OwnedAssets assets)
{
    public async Task<CareReminder> GetForUpdateAsync(Guid customerId, Guid reminderId, CancellationToken ct)
    {
        var reminder = await db.Set<CareReminder>().FirstOrDefaultAsync(r => r.Id == new CareReminderId(reminderId), ct)
            ?? throw new NotFoundException(nameof(CareReminder), reminderId);
        await assets.GetAsync(customerId, reminder.AssetId.Value, ct);   // 404 unless it is theirs
        return reminder;
    }
}

/// <summary>A reminder's status after the owner acted on it.</summary>
public sealed record CareReminderStatusResponse(Guid ReminderId, string Status);
