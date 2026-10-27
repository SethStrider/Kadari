using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry.Events;

/// <summary>Raised when a coverage rule version goes live (CARE/12 §11 RegistryRulePublished).</summary>
public sealed record RegistryRulePublishedDomainEvent(
    CoverageRuleVersionId RuleVersionId, Guid LogicalRuleId, int Version, DateTimeOffset At) : IDomainEvent;
