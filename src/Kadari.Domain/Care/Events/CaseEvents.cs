using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;

namespace Kadari.Domain.Care.Events;

/// <summary>The owner reported a problem (CARE/12 §11 AssetCaseOpened).</summary>
public sealed record AssetCaseOpenedDomainEvent(AssetCaseId CaseId, AssetId AssetId, bool SafetyFlagged, DateTimeOffset At)
    : IDomainEvent;

/// <summary>No verified route: a person must find one (CARE/11 unknown-route queue).</summary>
public sealed record CaseNeedsRouteDomainEvent(AssetCaseId CaseId, AssetId AssetId, DateTimeOffset At) : IDomainEvent;

/// <summary>A promise was added or settled — its reminder follows it.</summary>
public sealed record CaseCommitmentChangedDomainEvent(
    AssetCaseId CaseId, AssetId AssetId, CaseCommitmentId CommitmentId, DateTimeOffset At) : IDomainEvent;

/// <summary>An appointment was booked or settled — its reminder follows it.</summary>
public sealed record CaseAppointmentChangedDomainEvent(
    AssetCaseId CaseId, AssetId AssetId, CaseAppointmentId AppointmentId, DateTimeOffset At) : IDomainEvent;

/// <summary>The case closed (CARE/12 §11 AssetCaseResolved): resolved, rejected or withdrawn.</summary>
public sealed record AssetCaseClosedDomainEvent(
    AssetCaseId CaseId, AssetId AssetId, CaseStatus Status, CaseOutcome? Outcome, DateTimeOffset At) : IDomainEvent;
