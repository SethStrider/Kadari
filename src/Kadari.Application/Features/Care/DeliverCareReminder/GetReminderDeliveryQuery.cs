using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.Notifications;

namespace Kadari.Application.Features.Care.DeliverCareReminder;

/// <summary>
/// Who to tell about a queued reminder, how, and what to say — or why not. Only the asset's primary owner, only when
/// they opted in to Care messages, by email when they have one and SMS otherwise. Read by the Worker's consumer, which
/// does the send and then records the outcome.
/// </summary>
/// <remarks>Unscoped: the Worker acts as the system for a reminder it was handed; no customer calls this.</remarks>
public sealed record GetReminderDeliveryQuery(Guid ReminderId) : IQuery<ReminderDeliveryDto>, IUnscopedRequest;

/// <param name="Send">False when nothing should be sent (already handled, no opt-in, no contact).</param>
public sealed record ReminderDeliveryDto(bool Send, string? SkipReason, NotificationChannel? Channel, string? Recipient, string? Body);

internal sealed class GetReminderDeliveryHandler(IKadariDbContext db, IOptions<CareOptions> options)
    : IQueryHandler<GetReminderDeliveryQuery, ReminderDeliveryDto>
{
    public async Task<ReminderDeliveryDto> Handle(GetReminderDeliveryQuery query, CancellationToken ct)
    {
        var reminder = await db.Set<CareReminder>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == new CareReminderId(query.ReminderId), ct);
        if (reminder is not { Status: CareReminderStatus.Queued })
        {
            return Skip("Not queued");
        }

        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == reminder.AssetId, ct);
        var customerId = asset is null
            ? null
            : await db.Owners.AsNoTracking().Where(o => o.Id == asset.OwnerId).Select(o => o.CustomerId).FirstOrDefaultAsync(ct);
        var customer = customerId is null ? null : await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (asset is null || customer is null)
        {
            return Skip("No owner to tell");
        }

        var optedIn = await db.KadariConsents.AsNoTracking().AnyAsync(
            c => c.CustomerId == customer.Id && c.Scope == KadariConsentScope.CareReminders && c.Status == KadariConsentStatus.Granted, ct);
        if (!optedIn)
        {
            return Skip("Not opted in");
        }

        var keyDate = reminder.Type switch
        {
            CareReminderType.RegistrationDeadline => await db.Set<RegistrationOpportunity>().AsNoTracking()
                .Where(o => o.Id == new RegistrationOpportunityId(reminder.SourceId)).Select(o => o.DeadlineOn).FirstOrDefaultAsync(ct),
            CareReminderType.Warranty90 or CareReminderType.Warranty30 => await db.CareProfiles.AsNoTracking()
                .Where(p => p.AssetId == asset.Id).Select(p => p.CoverageExpiresOn).FirstOrDefaultAsync(ct),
            _ => reminder.DueOn,
        };

        var link = string.IsNullOrWhiteSpace(options.Value.AppBaseUrl)
            ? null
            : $"{options.Value.AppBaseUrl!.TrimEnd('/')}/{customer.Id.Value}/assets/{asset.Id.Value}/care";
        var body = ReminderMessages.Body(reminder.Type, asset.DisplayName, keyDate, link);

        return string.IsNullOrWhiteSpace(customer.Email)
            ? new ReminderDeliveryDto(true, null, NotificationChannel.Sms, customer.MobileNumber, body)
            : new ReminderDeliveryDto(true, null, NotificationChannel.Email, customer.Email, $"{ReminderMessages.Headline(reminder.Type)}\n\n{body}");
    }

    private static ReminderDeliveryDto Skip(string reason) => new(false, reason, null, null, null);
}
