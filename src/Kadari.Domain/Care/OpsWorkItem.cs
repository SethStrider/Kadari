using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;

namespace Kadari.Domain.Care;

public readonly record struct OpsWorkItemId(Guid Value)
{
    public static OpsWorkItemId New() => new(Guid.CreateVersion7());
}

/// <summary>The Care Ops queues (CARE/11 §4). Only those V1 raises are listed; more join as their triggers exist.</summary>
public enum OpsWorkType
{
    RouteUnknown,
    SafetyReview,
    RegistrationFailed,
    RuleConflict,
    CaseStalled,
}

/// <summary>CARE/11 §5: P0 immediate (safety), P1 urgent, P2 standard, P3 enrichment.</summary>
public enum OpsSeverity
{
    P0,
    P1,
    P2,
    P3,
}

public enum OpsWorkStatus
{
    Open,
    InProgress,
    Resolved,
}

/// <summary>Every resolution says what kind of knowledge it was (CARE/14 C12).</summary>
public enum OpsResolution
{
    ReusableKnowledge,
    CaseSpecific,
    PolicyReview,
    ProductBug,
}

/// <summary>
/// One exception a person must handle because automation cannot safely finish it (CARE/12 §9 OpsWorkItem, CARE/11).
/// Raised by the system, never by the customer; idempotent through <see cref="DedupeKey"/> (unique in the database),
/// so the same exception is queued once however often it is noticed. The summary is KADARI's own sentence — never the
/// customer's words or evidence (CARE/11 §22). Resolved with the kind of knowledge it produced; never deleted.
/// </summary>
public sealed class OpsWorkItem : AggregateRoot<OpsWorkItemId>
{
    public const int MaxSummaryLength = 280;
    public const int MaxNotesLength = 1000;

    private OpsWorkItem() { }   // EF

    private OpsWorkItem(OpsWorkItemId id) : base(id) { }

    public OpsWorkType WorkType { get; private init; }
    public OpsSeverity Severity { get; private init; }
    public AssetId? AssetId { get; private init; }
    public AssetCaseId? CaseId { get; private init; }
    public string ReasonCode { get; private init; } = null!;
    public string Summary { get; private init; } = null!;
    public string DedupeKey { get; private init; } = null!;
    public OpsWorkStatus Status { get; private set; }
    public Guid? AssignedTo { get; private set; }
    public DateTimeOffset RaisedAt { get; private init; }
    public DateTimeOffset? DueAt { get; private init; }
    public OpsResolution? Resolution { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static string KeyFor(OpsWorkType type, Guid sourceId) => $"{type}:{sourceId:N}";

    public static OpsWorkItem Raise(
        OpsWorkType type, OpsSeverity severity, AssetId? assetId, AssetCaseId? caseId, Guid sourceId, string reasonCode,
        string summary, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        return new OpsWorkItem(OpsWorkItemId.New())
        {
            WorkType = type,
            Severity = severity,
            AssetId = assetId,
            CaseId = caseId,
            ReasonCode = reasonCode.Trim(),
            Summary = AssetCase.Clip(summary, MaxSummaryLength),
            DedupeKey = KeyFor(type, sourceId),
            Status = OpsWorkStatus.Open,
            RaisedAt = at,
            // CARE/11 §5: P0 now, P1 within a day, P2 within three, P3 when there is time.
            DueAt = severity switch
            {
                OpsSeverity.P0 => at.AddHours(1),
                OpsSeverity.P1 => at.AddDays(1),
                OpsSeverity.P2 => at.AddDays(3),
                _ => null,
            },
        };
    }

    public void Take(Guid operatorId)
    {
        EnsureOpen();
        if (operatorId == Guid.Empty)
        {
            throw new DomainException("Only a signed-in operator can take a work item.");
        }

        AssignedTo = operatorId;
        Status = OpsWorkStatus.InProgress;
    }

    public void Resolve(Guid operatorId, OpsResolution resolution, string notes, DateTimeOffset at)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);
        if (operatorId == Guid.Empty)
        {
            throw new DomainException("Only a signed-in operator can resolve a work item.");
        }

        Resolution = resolution;
        ResolutionNotes = AssetCase.Clip(notes, MaxNotesLength);
        ResolvedBy = operatorId;
        ResolvedAt = at;
        AssignedTo ??= operatorId;
        Status = OpsWorkStatus.Resolved;
    }

    /// <summary>The thing it was about no longer needs a person (the case closed, the owner did it themselves).</summary>
    public void Close(string why, DateTimeOffset at)
    {
        if (Status == OpsWorkStatus.Resolved)
        {
            return;
        }

        Resolution = OpsResolution.CaseSpecific;
        ResolutionNotes = AssetCase.Clip(why, MaxNotesLength);
        ResolvedAt = at;
        Status = OpsWorkStatus.Resolved;
    }

    private void EnsureOpen()
    {
        if (Status == OpsWorkStatus.Resolved)
        {
            throw new DomainException("This work item is already resolved.");
        }
    }
}
