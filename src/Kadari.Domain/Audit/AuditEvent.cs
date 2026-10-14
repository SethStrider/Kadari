namespace Kadari.Domain.Audit;

public readonly record struct AuditEventId(Guid Value)
{
    public static AuditEventId New() => new(Guid.CreateVersion7());
}

public enum ActorKind
{
    Account,
    System,
    Connector,
    Reviewer,
}

/// <summary>
/// An append-only audit record (CDM-001, BR-013). Deliberately NOT an <c>Entity&lt;TId&gt;</c>: it is
/// immutable and never soft-deleted or updated. Recorded once via <see cref="Record"/>. The SQL
/// hardening (REVOKE UPDATE, DELETE) and an auto-writing interceptor are later slices.
/// </summary>
public sealed class AuditEvent
{
    private AuditEvent() { }   // EF

    private AuditEvent(
        AuditEventId id, ActorKind actorKind, Guid? actorId, string subjectType, Guid subjectId,
        string action, string? previousState, string? newState, DateTimeOffset occurredAt, string? ipAddress)
    {
        Id = id;
        ActorKind = actorKind;
        ActorId = actorId;
        OwnerType = subjectType;
        OwnerId = subjectId;
        Action = action;
        PreviousState = previousState;
        NewState = newState;
        OccurredAt = occurredAt;
        IpAddress = ipAddress;
    }

    public AuditEventId Id { get; private init; }
    public ActorKind ActorKind { get; private init; }
    public Guid? ActorId { get; private init; }
    public string OwnerType { get; private init; } = null!;
    public Guid OwnerId { get; private init; }
    public string Action { get; private init; } = null!;
    public string? PreviousState { get; private init; }
    public string? NewState { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }
    public string? IpAddress { get; private init; }

    public static AuditEvent Record(
        ActorKind actorKind, string subjectType, Guid subjectId, string action, DateTimeOffset occurredAt,
        Guid? actorId = null, string? previousState = null, string? newState = null, string? ipAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        return new AuditEvent(
            AuditEventId.New(), actorKind, actorId, subjectType.Trim(), subjectId, action.Trim(),
            previousState, newState, occurredAt, ipAddress);
    }
}
