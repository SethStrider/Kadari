using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care.Events;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

public readonly record struct AssetCaseId(Guid Value)
{
    public static AssetCaseId New() => new(Guid.CreateVersion7());
}

/// <summary>What a case is about (CARE/12 §10 CaseType).</summary>
public enum CaseType
{
    Warranty,
    Repair,
    Return,
    Recall,
    Insurance,
    Maintenance,
    Replacement,
    OwnershipTransfer,
    Other,
}

/// <summary>Where a case is (CARE/12 §10 CaseStatus). The last three are closed.</summary>
public enum CaseStatus
{
    Preparing,
    Sent,
    WaitingProvider,
    WaitingCustomer,
    AppointmentBooked,
    Collected,
    Assessing,
    RepairUnderway,
    WaitingPart,
    Ready,
    Rejected,
    Resolved,
    ClosedWithdrawn,
}

/// <summary>How a case ended (CARE/14 C11).</summary>
public enum CaseOutcome
{
    Repaired,
    Replaced,
    Refunded,
    Credited,
    NoFaultFound,
    Rejected,
    ResolvedOther,
}

/// <summary>
/// One after-sales problem with one asset (CARE/12 §3 AssetCase, CARE/14 C9). Generic: the same case carries a retailer
/// return, an OEM repair or a service-centre visit — the route says which. Holds the owner's description, the safety
/// flag, the route KADARI found (from a recorded <see cref="RoutingDecision"/>), the references the provider gave, the
/// promises and appointments made, the messages handed off, and the outcome. Every material change appends a
/// <see cref="CaseEvent"/> to the timeline; closing raises an event that writes the outcome to the asset's history.
/// </summary>
public sealed class AssetCase : AggregateRoot<AssetCaseId>
{
    public const int MaxSummaryLength = 2000;
    public const int MaxTitleLength = 120;
    public const int MaxNotesLength = 1000;
    public const string Redacted = "[erased]";

    private readonly List<CaseEvent> _events = [];
    private readonly List<CaseReference> _references = [];
    private readonly List<CaseCommitment> _commitments = [];
    private readonly List<CaseAppointment> _appointments = [];
    private readonly List<CaseMessage> _messages = [];

    private AssetCase() { }   // EF

    private AssetCase(AssetCaseId id) : base(id) { }

    public AssetId AssetId { get; private init; }
    public CaseType CaseType { get; private set; }
    public string Title { get; private set; } = null!;
    public string IssueSummary { get; private set; } = null!;
    public IssueClass IssueClass { get; private init; }
    public bool SafetyFlagged { get; private init; }

    /// <summary>The cover when the problem was reported — what the route was decided on.</summary>
    public CoverageState CoverageAtOpen { get; private init; }

    public RoutingDecisionId? RoutingDecisionId { get; private set; }
    public RouteType RouteType { get; private set; }
    public RouteConfidence RouteConfidence { get; private set; }

    /// <summary>The owner has accepted the route (a green route is accepted when found).</summary>
    public bool RouteAccepted { get; private set; }

    public string? TargetName { get; private set; }
    public ContactChannelType? ChannelType { get; private set; }
    public string? ChannelValue { get; private set; }
    public string? Instructions { get; private set; }
    public RouteEvidence[] RequiredEvidence { get; private set; } = [];

    public CaseStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private init; }

    /// <summary>The earliest open promise or appointment — when something should next happen.</summary>
    public DateOnly? NextActionOn { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }
    public CaseOutcome? Outcome { get; private set; }
    public string? OutcomeNotes { get; private set; }

    public IReadOnlyList<CaseEvent> Events => _events.AsReadOnly();
    public IReadOnlyList<CaseReference> References => _references.AsReadOnly();
    public IReadOnlyList<CaseCommitment> Commitments => _commitments.AsReadOnly();
    public IReadOnlyList<CaseAppointment> Appointments => _appointments.AsReadOnly();
    public IReadOnlyList<CaseMessage> Messages => _messages.AsReadOnly();

    /// <summary>A short number the owner can quote ("KD-04821"). For display and conversation only — the case id is
    /// what identifies it.</summary>
    public string Reference => ReferenceOf(Id);

    public static string ReferenceOf(AssetCaseId id) =>
        $"KD-{BitConverter.ToUInt32(id.Value.ToByteArray(), 12) % 100000:D5}";

    public bool IsClosed => Status is CaseStatus.Resolved or CaseStatus.Rejected or CaseStatus.ClosedWithdrawn;

    /// <summary>A person has to find the route before anything can be sent.</summary>
    public bool NeedsRoute => RouteType == RouteType.ResearchRequired;

    /// <summary>
    /// The owner reported a problem. The case opens in <see cref="CaseStatus.Preparing"/> with no route yet; when the
    /// safety gate tripped, the timeline says so first.
    /// </summary>
    public static AssetCase Open(
        AssetId assetId, IssueClass issueClass, string description, bool safetyFlagged, CoverageState coverage,
        DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var summary = description.Trim();
        if (summary.Length > MaxSummaryLength)
        {
            throw new DomainException($"Describe the problem in at most {MaxSummaryLength} characters.");
        }

        if (issueClass == IssueClass.Any)
        {
            throw new DomainException("Say what kind of problem it is.");
        }

        var inCover = coverage is CoverageState.Active or CoverageState.Expiring or CoverageState.LikelyActive;
        var @case = new AssetCase(AssetCaseId.New())
        {
            AssetId = assetId,
            CaseType = inCover && issueClass != IssueClass.AccidentalDamage ? CaseType.Warranty : CaseType.Repair,
            Title = TitleFrom(summary),
            IssueSummary = summary,
            IssueClass = issueClass,
            SafetyFlagged = safetyFlagged,
            CoverageAtOpen = coverage,
            RouteType = RouteType.ResearchRequired,
            RouteConfidence = RouteConfidence.Red,
            Status = CaseStatus.Preparing,
            OpenedAt = at,
        };

        @case.Log(CaseEventType.Opened, CaseActor.Owner, "You reported a problem.", at);
        if (safetyFlagged)
        {
            @case.Log(CaseEventType.SafetyStop, CaseActor.System, "KADARI showed stop-use safety guidance first.", at);
        }

        @case.RaiseDomainEvent(new AssetCaseOpenedDomainEvent(@case.Id, assetId, safetyFlagged, at));
        return @case;
    }

    /// <summary>
    /// Adopt a recorded routing decision and, when it matched one, the rule's counterparty details. Only while the case
    /// is still being prepared — once something was sent, the route is history. A green route is accepted outright; an
    /// amber one waits for the owner; research-required waits for a person (Care Ops).
    /// </summary>
    public void ApplyRoute(RoutingDecision decision, RoutingRuleVersion? rule, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.AssetId != AssetId)
        {
            throw new DomainException("A routing decision can only be applied to a case on its own asset.");
        }

        if (Status != CaseStatus.Preparing)
        {
            throw new DomainException("The route can only change while the case is being prepared.");
        }

        if (rule is not null && decision.MatchedRuleVersionId != rule.Id)
        {
            throw new DomainException("The rule must be the one the decision matched.");
        }

        RoutingDecisionId = decision.Id;
        RouteType = decision.RouteType;
        RouteConfidence = decision.Confidence;
        RouteAccepted = decision.Confidence == RouteConfidence.Green && decision.RouteType != RouteType.ResearchRequired;
        TargetName = rule?.TargetName;
        ChannelType = rule?.ChannelType;
        ChannelValue = rule?.ChannelValue;
        Instructions = rule?.Instructions;
        RequiredEvidence = rule?.RequiredEvidence ?? [];

        if (NeedsRoute)
        {
            Log(CaseEventType.RouteNeedsResearch, CaseActor.System, "A KADARI Care specialist will find the right place for this.", at);
            RaiseDomainEvent(new CaseNeedsRouteDomainEvent(Id, AssetId, at));
        }
        else
        {
            Log(CaseEventType.RouteFound, decision.HumanOverride ? CaseActor.Operator : CaseActor.System,
                $"Route: {Describe(RouteType)}{(TargetName is null ? "" : $" — {TargetName}")}.", at);
        }
    }

    /// <summary>A person confirmed the route by hand (no registry rule yet): the counterparty as they found it.</summary>
    public void ApplyManualRoute(
        RoutingDecision decision, string targetName, ContactChannelType channelType, string? channelValue,
        string instructions, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.HumanOverride)
        {
            throw new DomainException("A manual route comes from a person's decision.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        if (channelType != ContactChannelType.InStore && string.IsNullOrWhiteSpace(channelValue))
        {
            throw new DomainException("Say how to reach the counterparty.");
        }

        ApplyRoute(decision, null, at);
        TargetName = Clip(targetName, 200);
        ChannelType = channelType;
        ChannelValue = string.IsNullOrWhiteSpace(channelValue) ? null : Clip(channelValue, 500);
        Instructions = Clip(instructions, 1000);
        RouteAccepted = false;   // the owner still confirms what a person found
    }

    public void AcceptRoute(DateTimeOffset at)
    {
        EnsureOpen();
        if (NeedsRoute)
        {
            throw new DomainException("There is no route to accept yet.");
        }

        if (RouteAccepted)
        {
            return;
        }

        RouteAccepted = true;
        Log(CaseEventType.RouteAccepted, CaseActor.Owner, "You confirmed where this goes.", at);
    }

    /// <summary>
    /// The owner sent the prepared case pack through the provider's own channel. Idempotent on
    /// <paramref name="idempotencyKey"/>: a retried request returns the message already recorded, so a handoff is
    /// never recorded twice.
    /// </summary>
    public CaseMessage MarkSent(string channel, string sharedSummary, string idempotencyKey, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var key = idempotencyKey.Trim();
        var replay = _messages.FirstOrDefault(m => m.IdempotencyKey == key);
        if (replay is not null)
        {
            return replay;
        }

        EnsureOpen();
        if (!RouteAccepted || NeedsRoute)
        {
            throw new DomainException("Confirm where this goes before sending it.");
        }

        var message = CaseMessage.Outbound(Id, channel, sharedSummary, key, at);
        _messages.Add(message);
        if (Status == CaseStatus.Preparing)
        {
            Status = CaseStatus.Sent;
        }

        Log(CaseEventType.Sent, CaseActor.Owner, $"You sent the details to {TargetName ?? "the provider"}.", at);
        return message;
    }

    public CaseReference AddReference(string party, string reference, DateTimeOffset at)
    {
        EnsureOpen();
        var added = CaseReference.Create(party, reference, at);
        if (_references.Any(r => r.Party == added.Party && r.Reference == added.Reference))
        {
            return _references.First(r => r.Party == added.Party && r.Reference == added.Reference);
        }

        _references.Add(added);
        Log(CaseEventType.ReferenceAdded, CaseActor.Owner, $"Reference from {added.Party}: {added.Reference}.", at);
        return added;
    }

    private static readonly CaseStatus[] ProviderStatuses =
    [
        CaseStatus.WaitingProvider, CaseStatus.WaitingCustomer, CaseStatus.Collected, CaseStatus.Assessing,
        CaseStatus.RepairUnderway, CaseStatus.WaitingPart, CaseStatus.Ready,
    ];

    /// <summary>The provider replied or moved the case on — captured by the owner until integrations exist.</summary>
    public void RecordProviderUpdate(CaseStatus status, string? note, DateTimeOffset at)
    {
        EnsureOpen();
        if (!ProviderStatuses.Contains(status))
        {
            throw new DomainException($"{status} is not a provider update; close the case to record an outcome.");
        }

        if (Status == CaseStatus.Preparing)
        {
            throw new DomainException("Send the case before recording the provider's reply.");
        }

        Status = status;
        if (!string.IsNullOrWhiteSpace(note))
        {
            _messages.Add(CaseMessage.Inbound(Id, note, at));
        }

        Log(CaseEventType.ProviderUpdate, CaseActor.Provider, $"Provider update: {Describe(status)}.", at);
    }

    /// <summary>A promise — "we'll call you within 5 working days" — that KADARI will remember.</summary>
    public CaseCommitment AddCommitment(CommitmentParty party, string description, DateOnly dueOn, DateTimeOffset at)
    {
        EnsureOpen();
        if (dueOn < DateOnly.FromDateTime(at.UtcDateTime))
        {
            throw new DomainException("A promise is for today or later.");
        }

        var commitment = CaseCommitment.Create(party, description, dueOn);
        _commitments.Add(commitment);
        RefreshNextAction();
        Log(CaseEventType.CommitmentAdded, party == CommitmentParty.Provider ? CaseActor.Provider : CaseActor.Owner,
            $"Promise: {commitment.Description} by {dueOn:d MMMM yyyy}.", at);
        RaiseDomainEvent(new CaseCommitmentChangedDomainEvent(Id, AssetId, commitment.Id, at));
        return commitment;
    }

    public void KeepCommitment(CaseCommitmentId id, DateTimeOffset at)
    {
        var commitment = _commitments.FirstOrDefault(c => c.Id == id) ?? throw new DomainException("No such promise on this case.");
        commitment.Keep(at);
        RefreshNextAction();
        Log(CaseEventType.CommitmentKept, CaseActor.Owner, $"Promise kept: {commitment.Description}.", at);
        RaiseDomainEvent(new CaseCommitmentChangedDomainEvent(Id, AssetId, commitment.Id, at));
    }

    public CaseAppointment BookAppointment(
        DateTimeOffset startsAt, DateTimeOffset? endsAt, string? location, string? instructions, DateTimeOffset at)
    {
        EnsureOpen();
        if (Status == CaseStatus.Preparing)
        {
            throw new DomainException("Send the case before booking an appointment.");
        }

        var appointment = CaseAppointment.Book(startsAt, endsAt, location, instructions, at);
        _appointments.Add(appointment);
        Status = CaseStatus.AppointmentBooked;
        RefreshNextAction();
        Log(CaseEventType.AppointmentBooked, CaseActor.Owner, $"Appointment booked for {startsAt:d MMMM yyyy}.", at);
        RaiseDomainEvent(new CaseAppointmentChangedDomainEvent(Id, AssetId, appointment.Id, at));
        return appointment;
    }

    public void CompleteAppointment(CaseAppointmentId id, DateTimeOffset at) => SettleAppointment(id, done: true, at);

    public void CancelAppointment(CaseAppointmentId id, DateTimeOffset at) => SettleAppointment(id, done: false, at);

    /// <summary>
    /// The case ended. A rejected claim closes as <see cref="CaseStatus.Rejected"/>, anything else as
    /// <see cref="CaseStatus.Resolved"/>. Open promises and appointments are cancelled — nothing is left to chase.
    /// </summary>
    public void Resolve(CaseOutcome outcome, string? notes, DateTimeOffset at)
    {
        EnsureOpen();
        Outcome = outcome;
        OutcomeNotes = string.IsNullOrWhiteSpace(notes) ? null : Clip(notes, MaxNotesLength);
        Close(outcome == CaseOutcome.Rejected ? CaseStatus.Rejected : CaseStatus.Resolved, at);
        Log(CaseEventType.Resolved, CaseActor.Owner, $"Closed: {Describe(outcome)}.", at);
        RaiseDomainEvent(new AssetCaseClosedDomainEvent(Id, AssetId, Status, outcome, at));
    }

    /// <summary>The owner no longer needs this case.</summary>
    public void Withdraw(DateTimeOffset at)
    {
        EnsureOpen();
        Close(CaseStatus.ClosedWithdrawn, at);
        Log(CaseEventType.Withdrawn, CaseActor.Owner, "You closed this case.", at);
        RaiseDomainEvent(new AssetCaseClosedDomainEvent(Id, AssetId, Status, null, at));
    }

    /// <summary>The owner was erased (POPIA): forget what they wrote, keep that a case existed and how it ended.</summary>
    public void Redact()
    {
        IssueSummary = Redacted;
        Title = Redacted;
        OutcomeNotes = OutcomeNotes is null ? null : Redacted;
        foreach (var message in _messages)
        {
            message.Redact();
        }

        foreach (var commitment in _commitments)
        {
            commitment.Redact();
        }

        foreach (var appointment in _appointments)
        {
            appointment.Redact();
        }

        foreach (var reference in _references)
        {
            reference.Redact();
        }
    }

    public static string Describe(RouteType route) => route switch
    {
        RouteType.RetailerReturn => "return it to the store",
        RouteType.RetailerCredit => "the store gives credit or a swap",
        RouteType.OemDirect => "contact the manufacturer",
        RouteType.WarrantyAdmin => "the warranty administrator",
        RouteType.AuthorisedService => "an authorised service centre",
        RouteType.Onsite => "a technician comes to you",
        RouteType.CarryIn => "take it to a service centre",
        RouteType.Collection => "it is collected from you",
        RouteType.ProtectionProvider => "your protection plan provider",
        _ => "being researched",
    };

    public static string Describe(CaseStatus status) => status switch
    {
        CaseStatus.Preparing => "being prepared",
        CaseStatus.Sent => "sent",
        CaseStatus.WaitingProvider => "waiting for the provider",
        CaseStatus.WaitingCustomer => "waiting for you",
        CaseStatus.AppointmentBooked => "appointment booked",
        CaseStatus.Collected => "collected",
        CaseStatus.Assessing => "being assessed",
        CaseStatus.RepairUnderway => "repair underway",
        CaseStatus.WaitingPart => "waiting for a part",
        CaseStatus.Ready => "ready",
        CaseStatus.Rejected => "rejected",
        CaseStatus.Resolved => "resolved",
        _ => "closed",
    };

    public static string Describe(CaseOutcome outcome) => outcome switch
    {
        CaseOutcome.Repaired => "repaired",
        CaseOutcome.Replaced => "replaced",
        CaseOutcome.Refunded => "refunded",
        CaseOutcome.Credited => "store credit given",
        CaseOutcome.NoFaultFound => "no fault found",
        CaseOutcome.Rejected => "claim rejected",
        _ => "resolved",
    };

    private void SettleAppointment(CaseAppointmentId id, bool done, DateTimeOffset at)
    {
        var appointment = _appointments.FirstOrDefault(a => a.Id == id) ?? throw new DomainException("No such appointment on this case.");
        if (done)
        {
            appointment.Complete(at);
        }
        else
        {
            appointment.Cancel(at);
        }

        if (Status == CaseStatus.AppointmentBooked && !_appointments.Any(a => a.Status == AppointmentStatus.Booked))
        {
            Status = done ? CaseStatus.Assessing : CaseStatus.WaitingProvider;
        }

        RefreshNextAction();
        Log(done ? CaseEventType.AppointmentDone : CaseEventType.AppointmentCancelled, CaseActor.Owner,
            done ? "Appointment done." : "Appointment cancelled.", at);
        RaiseDomainEvent(new CaseAppointmentChangedDomainEvent(Id, AssetId, appointment.Id, at));
    }

    private void Close(CaseStatus status, DateTimeOffset at)
    {
        foreach (var commitment in _commitments.Where(c => c.Status == CommitmentStatus.Open))
        {
            commitment.Cancel(at);
        }

        foreach (var appointment in _appointments.Where(a => a.Status == AppointmentStatus.Booked))
        {
            appointment.Cancel(at);
        }

        Status = status;
        ClosedAt = at;
        NextActionOn = null;
    }

    private void RefreshNextAction()
    {
        var dates = _commitments.Where(c => c.Status == CommitmentStatus.Open).Select(c => c.DueOn)
            .Concat(_appointments.Where(a => a.Status == AppointmentStatus.Booked).Select(a => DateOnly.FromDateTime(a.StartsAt.UtcDateTime)))
            .ToList();
        NextActionOn = dates.Count == 0 ? null : dates.Min();
    }

    private void EnsureOpen()
    {
        if (IsClosed)
        {
            throw new DomainException($"This case is {Describe(Status)}.");
        }
    }

    private void Log(CaseEventType type, CaseActor actor, string summary, DateTimeOffset at) =>
        _events.Add(CaseEvent.Record(_events.Count + 1, type, actor, summary, at));

    private static string TitleFrom(string summary)
    {
        var firstLine = summary.Split('\n', 2)[0].Trim();
        return firstLine.Length <= MaxTitleLength ? firstLine : firstLine[..(MaxTitleLength - 1)] + "…";
    }

    internal static string Clip(string value, int max) => value.Trim() is var t && t.Length > max ? t[..max] : value.Trim();
}
