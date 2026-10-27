using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Care;

public readonly record struct CaseEventId(Guid Value);
public readonly record struct CaseReferenceId(Guid Value);
public readonly record struct CaseCommitmentId(Guid Value);
public readonly record struct CaseAppointmentId(Guid Value);
public readonly record struct CaseMessageId(Guid Value);

public enum CaseEventType
{
    Opened,
    SafetyStop,
    RouteFound,
    RouteNeedsResearch,
    RouteAccepted,
    Sent,
    ReferenceAdded,
    ProviderUpdate,
    CommitmentAdded,
    CommitmentKept,
    AppointmentBooked,
    AppointmentDone,
    AppointmentCancelled,
    Resolved,
    Withdrawn,
}

public enum CaseActor
{
    Owner,
    Provider,
    Operator,
    System,
}

/// <summary>
/// One entry on a case's timeline (CARE/12 §3 CaseEvent). Append-only: the case adds them and never changes one. The
/// summary is KADARI's own sentence — it never quotes what the owner or provider wrote.
/// </summary>
public sealed class CaseEvent
{
    private CaseEvent() { }   // EF

    public CaseEventId Id { get; private init; }

    /// <summary>Position on the timeline, from 1 — entries recorded in the same instant keep their order.</summary>
    public int Sequence { get; private init; }

    public CaseEventType Type { get; private init; }
    public CaseActor Actor { get; private init; }
    public string Summary { get; private init; } = null!;
    public DateTimeOffset OccurredAt { get; private init; }

    internal static CaseEvent Record(int sequence, CaseEventType type, CaseActor actor, string summary, DateTimeOffset at) => new()
    {
        Id = new CaseEventId(Guid.CreateVersion7()),
        Sequence = sequence,
        Type = type,
        Actor = actor,
        Summary = AssetCase.Clip(summary, 280),
        OccurredAt = at,
    };
}

/// <summary>A reference number a provider gave — "ticket 4471-22" — added whenever it arrives (CARE/14 C9).</summary>
public sealed class CaseReference : Entity<CaseReferenceId>
{
    public const int MaxLength = 120;

    private CaseReference() { }   // EF

    private CaseReference(CaseReferenceId id) : base(id) { }

    public string Party { get; private set; } = null!;
    public string Reference { get; private set; } = null!;
    public DateTimeOffset AddedAt { get; private init; }

    internal static CaseReference Create(string party, string reference, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(party);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (party.Trim().Length > MaxLength || reference.Trim().Length > MaxLength)
        {
            throw new DomainException($"A reference and who gave it are at most {MaxLength} characters each.");
        }

        return new CaseReference(new CaseReferenceId(Guid.CreateVersion7()))
        {
            Party = party.Trim(),
            Reference = reference.Trim(),
            AddedAt = at,
        };
    }

    internal void Redact() => Reference = AssetCase.Redacted;
}

public enum CommitmentParty
{
    Provider,
    Owner,
}

public enum CommitmentStatus
{
    Open,
    Kept,
    Cancelled,
}

/// <summary>A promise or a required next step with a date (CARE/12 §3 CaseCommitment). Overdue is derived — open and
/// past its date — and the reminder sweep chases it.</summary>
public sealed class CaseCommitment : Entity<CaseCommitmentId>
{
    public const int MaxDescriptionLength = 280;

    private CaseCommitment() { }   // EF

    private CaseCommitment(CaseCommitmentId id) : base(id) { }

    public CommitmentParty Party { get; private init; }
    public string Description { get; private set; } = null!;
    public DateOnly DueOn { get; private init; }
    public CommitmentStatus Status { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }

    public bool IsOverdueOn(DateOnly today) => Status == CommitmentStatus.Open && DueOn < today;

    internal static CaseCommitment Create(CommitmentParty party, string description, DateOnly dueOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Trim().Length > MaxDescriptionLength)
        {
            throw new DomainException($"Describe the promise in at most {MaxDescriptionLength} characters.");
        }

        return new CaseCommitment(new CaseCommitmentId(Guid.CreateVersion7()))
        {
            Party = party,
            Description = description.Trim(),
            DueOn = dueOn,
            Status = CommitmentStatus.Open,
        };
    }

    internal void Keep(DateTimeOffset at)
    {
        if (Status != CommitmentStatus.Open)
        {
            throw new DomainException($"This promise is already {Status}.");
        }

        Status = CommitmentStatus.Kept;
        SettledAt = at;
    }

    internal void Cancel(DateTimeOffset at)
    {
        Status = CommitmentStatus.Cancelled;
        SettledAt = at;
    }

    internal void Redact() => Description = AssetCase.Redacted;
}

public enum AppointmentStatus
{
    Booked,
    Done,
    Cancelled,
}

/// <summary>A visit, collection or drop-off (CARE/12 §3 CaseAppointment).</summary>
public sealed class CaseAppointment : Entity<CaseAppointmentId>
{
    public const int MaxTextLength = 500;

    private CaseAppointment() { }   // EF

    private CaseAppointment(CaseAppointmentId id) : base(id) { }

    public DateTimeOffset StartsAt { get; private init; }
    public DateTimeOffset? EndsAt { get; private init; }
    public string? Location { get; private set; }
    public string? Instructions { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }

    internal static CaseAppointment Book(
        DateTimeOffset startsAt, DateTimeOffset? endsAt, string? location, string? instructions, DateTimeOffset at)
    {
        if (endsAt is not null && endsAt <= startsAt)
        {
            throw new DomainException("An appointment ends after it starts.");
        }

        if (startsAt < at.AddDays(-1))
        {
            throw new DomainException("Book an appointment for today or later.");
        }

        return new CaseAppointment(new CaseAppointmentId(Guid.CreateVersion7()))
        {
            StartsAt = startsAt,
            EndsAt = endsAt,
            Location = string.IsNullOrWhiteSpace(location) ? null : AssetCase.Clip(location, MaxTextLength),
            Instructions = string.IsNullOrWhiteSpace(instructions) ? null : AssetCase.Clip(instructions, MaxTextLength),
            Status = AppointmentStatus.Booked,
        };
    }

    internal void Complete(DateTimeOffset at) => Settle(AppointmentStatus.Done, at);

    internal void Cancel(DateTimeOffset at) => Settle(AppointmentStatus.Cancelled, at);

    private void Settle(AppointmentStatus status, DateTimeOffset at)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainException($"This appointment is already {Status}.");
        }

        Status = status;
        SettledAt = at;
    }

    internal void Redact()
    {
        Location = Location is null ? null : AssetCase.Redacted;
        Instructions = Instructions is null ? null : AssetCase.Redacted;
    }
}

public enum MessageDirection
{
    Outbound,
    Inbound,
}

/// <summary>
/// A message on a case (CARE/12 §3 CaseMessage): a handoff the owner sent through the provider's channel (with the
/// summary of what was shared, never the documents), or a reply they captured. An outbound message carries an
/// idempotency key unique per case, so a retried "mark sent" is recorded once.
/// </summary>
public sealed class CaseMessage
{
    public const int MaxBodyLength = 2000;

    private CaseMessage() { }   // EF

    public CaseMessageId Id { get; private init; }
    public AssetCaseId CaseId { get; private init; }
    public MessageDirection Direction { get; private init; }
    public string Channel { get; private init; } = null!;
    public string Body { get; private set; } = null!;
    public string? IdempotencyKey { get; private init; }
    public DateTimeOffset At { get; private init; }

    internal static CaseMessage Outbound(AssetCaseId caseId, string channel, string sharedSummary, string key, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedSummary);
        return new CaseMessage
        {
            Id = new CaseMessageId(Guid.CreateVersion7()),
            CaseId = caseId,
            Direction = MessageDirection.Outbound,
            Channel = AssetCase.Clip(channel, 32),
            Body = AssetCase.Clip(sharedSummary, MaxBodyLength),
            IdempotencyKey = AssetCase.Clip(key, 100),
            At = at,
        };
    }

    internal static CaseMessage Inbound(AssetCaseId caseId, string note, DateTimeOffset at) => new()
    {
        Id = new CaseMessageId(Guid.CreateVersion7()),
        CaseId = caseId,
        Direction = MessageDirection.Inbound,
        Channel = "Captured",
        Body = AssetCase.Clip(note, MaxBodyLength),
        At = at,
    };

    internal void Redact() => Body = AssetCase.Redacted;
}
