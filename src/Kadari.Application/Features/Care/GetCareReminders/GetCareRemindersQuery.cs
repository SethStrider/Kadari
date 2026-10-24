using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.GetCareReminders;

/// <summary>
/// The Care reminders that need the owner's attention now, across their ledger or for one asset (CARE/05 C07, CARE/13
/// §6) — each with why it matters and the asset it is about, for the notification's deep link. Due reminders show here
/// whether or not a message was sent. Customer-scoped.
/// </summary>
public sealed record GetCareRemindersQuery(Guid CustomerId, Guid? AssetId) : IQuery<IReadOnlyList<CareReminderDto>>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record CareReminderDto(
    Guid ReminderId, Guid AssetId, string AssetName, string Type, string Headline, string Message, DateOnly DueOn, string Status);

internal sealed class GetCareRemindersHandler(IKadariDbContext db, OwnedAssets assets, TimeProvider clock)
    : IQueryHandler<GetCareRemindersQuery, IReadOnlyList<CareReminderDto>>
{
    public async Task<IReadOnlyList<CareReminderDto>> Handle(GetCareRemindersQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var ids = await assets.IdsAsync(query.CustomerId, ct);
        if (query.AssetId is { } only)
        {
            ids = ids.Where(id => id.Value == only).ToList();
        }

        var reminders = (await db.Set<CareReminder>().AsNoTracking()
                .Where(r => ids.Contains(r.AssetId)
                            && r.Status != CareReminderStatus.Cancelled && r.Status != CareReminderStatus.Dismissed
                            && r.Status != CareReminderStatus.Completed)
                .ToListAsync(ct))
            .Where(r => r.IsDueOn(today))
            .OrderBy(r => r.DueOn)
            .ToList();

        var assetIds = reminders.Select(r => r.AssetId).Distinct().ToList();
        var names = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.DisplayName, ct);
        var expiries = await db.CareProfiles.AsNoTracking().Where(p => assetIds.Contains(p.AssetId))
            .ToDictionaryAsync(p => p.AssetId, p => p.CoverageExpiresOn, ct);
        var offerIds = reminders.Where(r => r.Type == CareReminderType.RegistrationDeadline).Select(r => new RegistrationOpportunityId(r.SourceId)).ToList();
        var deadlines = await db.Set<RegistrationOpportunity>().AsNoTracking().Where(o => offerIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id.Value, o => o.DeadlineOn, ct);

        return reminders.Select(r =>
        {
            var name = names.GetValueOrDefault(r.AssetId) ?? "asset";
            var keyDate = r.Type == CareReminderType.RegistrationDeadline ? deadlines.GetValueOrDefault(r.SourceId) : expiries.GetValueOrDefault(r.AssetId);
            return new CareReminderDto(
                r.Id.Value, r.AssetId.Value, name, r.Type.ToString(), ReminderMessages.Headline(r.Type),
                ReminderMessages.Body(r.Type, name, keyDate, link: null)["KADARI: ".Length..], r.DueOn, r.Status.ToString());
        }).ToList();
    }
}
