using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;

namespace Kadari.Domain.Care;

public readonly record struct CareReminderId(Guid Value)
{
    public static CareReminderId New() => new(Guid.CreateVersion7());
}

/// <summary>The reminders Care sends (CARE/05 C07, CARE/03 §8). Case reminders arrive with the case engine.</summary>
public enum CareReminderType
{
    RegistrationDeadline,
    Warranty90,
    Warranty30,
    CaseFollowUp,
    CommitmentOverdue,
    AppointmentDue,
}

public enum CareReminderStatus
{
    /// <summary>Waiting for its date.</summary>
    Scheduled,

    /// <summary>Due; handed to the Worker for delivery.</summary>
    Queued,

    /// <summary>Delivered over a channel.</summary>
    Sent,

    /// <summary>Due but not sent — the owner has not opted in to Care messages. It still shows in the app.</summary>
    NotSent,

    Snoozed,
    Dismissed,
    Completed,

    /// <summary>No longer true (the dates moved, the registration was done). Never shown or sent.</summary>
    Cancelled,
}

/// <summary>
/// One reminder about an asset (CARE/12 §6 CareReminder): what it is about, when it is due, and where it is. Creation
/// is idempotent — <see cref="DedupeKey"/> (asset, type, source, due date) is unique in the database, so re-evaluating
/// Care never schedules a second copy (CARE/14 C6 "duplicate reminders prevented"). Delivery state is kept so it is
/// auditable, and a reminder that stops being true is cancelled, never deleted.
/// </summary>
public sealed class CareReminder : AggregateRoot<CareReminderId>
{
    private CareReminder() { }   // EF

    private CareReminder(CareReminderId id) : base(id) { }

    public AssetId AssetId { get; private init; }
    public CareReminderType Type { get; private init; }
    public DateOnly DueOn { get; private init; }
    public string SourceType { get; private init; } = null!;
    public Guid SourceId { get; private init; }
    public string DedupeKey { get; private init; } = null!;
    public CareReminderStatus Status { get; private set; }
    public DateOnly? SnoozedUntil { get; private set; }
    public DateTimeOffset? QueuedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public string? DeliveredVia { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Whether the owner should see it now: due (or past due), and not closed.</summary>
    public bool IsDueOn(DateOnly today) =>
        Status is CareReminderStatus.Queued or CareReminderStatus.Sent or CareReminderStatus.NotSent
        || (Status == CareReminderStatus.Scheduled && DueOn <= today)
        || (Status == CareReminderStatus.Snoozed && SnoozedUntil <= today);

    public static string KeyFor(AssetId assetId, CareReminderType type, Guid sourceId, DateOnly dueOn) =>
        $"{assetId.Value:N}:{type}:{sourceId:N}:{dueOn:yyyyMMdd}";

    public static CareReminder Schedule(AssetId assetId, CareReminderType type, DateOnly dueOn, string sourceType, Guid sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        return new CareReminder(CareReminderId.New())
        {
            AssetId = assetId,
            Type = type,
            DueOn = dueOn,
            SourceType = sourceType.Trim(),
            SourceId = sourceId,
            DedupeKey = KeyFor(assetId, type, sourceId, dueOn),
            Status = CareReminderStatus.Scheduled,
        };
    }

    /// <summary>The date has come: hand it to delivery. Only a scheduled reminder that is due, or a snooze that ended.</summary>
    public bool Queue(DateOnly today, DateTimeOffset at)
    {
        var due = (Status == CareReminderStatus.Scheduled && DueOn <= today)
                  || (Status == CareReminderStatus.Snoozed && SnoozedUntil <= today);
        if (!due)
        {
            return false;
        }

        Status = CareReminderStatus.Queued;
        QueuedAt = at;
        return true;
    }

    /// <summary>Delivered. Idempotent for a redelivered message: a reminder is sent once.</summary>
    public bool MarkSent(string channel, DateTimeOffset at)
    {
        if (Status != CareReminderStatus.Queued)
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        Status = CareReminderStatus.Sent;
        DeliveredVia = channel.Trim();
        DeliveredAt = at;
        return true;
    }

    /// <summary>Due, but the owner has not opted in to messages: it waits in the app instead.</summary>
    public bool MarkNotSent()
    {
        if (Status != CareReminderStatus.Queued)
        {
            return false;
        }

        Status = CareReminderStatus.NotSent;
        return true;
    }

    public void Snooze(DateOnly until, DateOnly today)
    {
        if (!IsDueOn(today) && Status != CareReminderStatus.Scheduled)
        {
            throw new DomainException($"A {Status} reminder cannot be snoozed.");
        }

        if (until <= today)
        {
            throw new DomainException("Snooze to a later day.");
        }

        Status = CareReminderStatus.Snoozed;
        SnoozedUntil = until;
    }

    public void Dismiss(DateTimeOffset at) => Close(CareReminderStatus.Dismissed, at);

    public void Complete(DateTimeOffset at) => Close(CareReminderStatus.Completed, at);

    /// <summary>It is no longer true. Only a reminder that has not reached the owner is cancelled.</summary>
    public void Cancel(DateTimeOffset at)
    {
        if (Status is CareReminderStatus.Scheduled or CareReminderStatus.Snoozed)
        {
            Status = CareReminderStatus.Cancelled;
            ClosedAt = at;
        }
    }

    private void Close(CareReminderStatus status, DateTimeOffset at)
    {
        if (Status is CareReminderStatus.Dismissed or CareReminderStatus.Completed or CareReminderStatus.Cancelled)
        {
            throw new DomainException($"This reminder is already {Status}.");
        }

        Status = status;
        ClosedAt = at;
    }
}

/// <summary>What reminders an asset should have (CARE/03 §8). Pure, so the schedule is deterministic and testable.</summary>
public static class ReminderPlan
{
    public sealed record Planned(CareReminderType Type, DateOnly DueOn, string SourceType, Guid SourceId);

    /// <summary>
    /// 90 and 30 days before the headline cover ends — only for cover KADARI stands behind (active or expiring); and for
    /// an open registration offer with a deadline, halfway there and three days before. Past dates are not planned:
    /// a reminder about something already gone is noise.
    /// </summary>
    public static IReadOnlyList<Planned> For(CareProfile profile, IEnumerable<RegistrationOpportunity> openOffers, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var plan = new List<Planned>();

        if (profile.CoverageExpiresOn is { } expiry && profile.CoverageState is CoverageState.Active or CoverageState.Expiring)
        {
            plan.Add(new Planned(CareReminderType.Warranty90, expiry.AddDays(-90), "CareProfile", profile.Id.Value));
            plan.Add(new Planned(CareReminderType.Warranty30, expiry.AddDays(-30), "CareProfile", profile.Id.Value));
        }

        foreach (var offer in openOffers.Where(o => o.IsOpen && o.DeadlineOn is not null))
        {
            var deadline = offer.DeadlineOn!.Value;
            var surfaced = DateOnly.FromDateTime(offer.SurfacedAt.UtcDateTime);
            var halfway = surfaced.AddDays((deadline.DayNumber - surfaced.DayNumber) / 2);
            plan.Add(new Planned(CareReminderType.RegistrationDeadline, halfway, "RegistrationOpportunity", offer.Id.Value));
            plan.Add(new Planned(CareReminderType.RegistrationDeadline, deadline.AddDays(-3), "RegistrationOpportunity", offer.Id.Value));
        }

        return plan.Where(p => p.DueOn >= today).DistinctBy(p => (p.Type, p.SourceId, p.DueOn)).ToList();
    }
}
