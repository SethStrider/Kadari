using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;

namespace Kadari.Domain.Care.Events;

/// <summary>Raised whenever a coverage decision is applied to a Care profile (CARE/12 §11 CoverageEvaluated). Carries
/// the before and after so reactions can tell a material change from a re-check that found the same thing.</summary>
public sealed record CoverageEvaluatedDomainEvent(
    CareProfileId CareProfileId,
    AssetId AssetId,
    CoverageDecisionId DecisionId,
    CoverageState PreviousState,
    CoverageState CurrentState,
    DateOnly? PreviousExpiresOn,
    DateOnly? CurrentExpiresOn,
    DateTimeOffset At) : IDomainEvent
{
    public bool IsMaterialChange => PreviousState != CurrentState || PreviousExpiresOn != CurrentExpiresOn;
}
