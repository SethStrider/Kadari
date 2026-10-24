using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

public readonly record struct RegistrationOpportunityId(Guid Value)
{
    public static RegistrationOpportunityId New() => new(Guid.CreateVersion7());
}

public enum OpportunityStatus
{
    Available,
    Dismissed,
    Taken,
    Expired,
}

/// <summary>
/// "Extra cover available" (CARE/05 C04, CARE/12 §5): a registration rule applies to this asset and has not been acted
/// on. One per asset and logical rule; re-evaluation refreshes it (a newer rule version, a newly known deadline) but
/// never resurrects one the owner dismissed or took.
/// </summary>
public sealed class RegistrationOpportunity : AggregateRoot<RegistrationOpportunityId>
{
    private RegistrationOpportunity() { }   // EF

    private RegistrationOpportunity(RegistrationOpportunityId id) : base(id) { }

    public AssetId AssetId { get; private init; }
    public Guid LogicalRuleId { get; private init; }
    public RegistrationRuleVersionId RuleVersionId { get; private set; }
    public OpportunityStatus Status { get; private set; }
    public string BenefitSummary { get; private set; } = null!;

    /// <summary>The last day to register, where the rule has a deadline and the purchase date is known.</summary>
    public DateOnly? DeadlineOn { get; private set; }

    public DateTimeOffset SurfacedAt { get; private init; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsOpen => Status == OpportunityStatus.Available;

    public static RegistrationOpportunity Surface(
        AssetId assetId, RegistrationRuleVersion rule, DateOnly? purchasedOn, DateTimeOffset at)
    {
        var opportunity = new RegistrationOpportunity(RegistrationOpportunityId.New())
        {
            AssetId = assetId,
            LogicalRuleId = rule.LogicalRuleId,
            SurfacedAt = at,
            Status = OpportunityStatus.Available,
        };
        opportunity.Refresh(rule, purchasedOn, at);
        return opportunity;
    }

    /// <summary>Follow the latest rule version and purchase date. An opportunity whose deadline has passed expires.</summary>
    public void Refresh(RegistrationRuleVersion rule, DateOnly? purchasedOn, DateTimeOffset at)
    {
        if (rule.LogicalRuleId != LogicalRuleId)
        {
            throw new DomainException("An opportunity follows one rule.");
        }

        if (!IsOpen)
        {
            return;
        }

        RuleVersionId = rule.Id;
        BenefitSummary = rule.BenefitDescription;
        DeadlineOn = rule.DeadlineDays is { } days && purchasedOn is { } bought ? bought.AddDays(days) : null;

        if (DeadlineOn is { } deadline && DateOnly.FromDateTime(at.UtcDateTime) > deadline)
        {
            Status = OpportunityStatus.Expired;
            ResolvedAt = at;
        }
    }

    public void Dismiss(DateTimeOffset at) => Close(OpportunityStatus.Dismissed, at);

    internal void Take(DateTimeOffset at) => Close(OpportunityStatus.Taken, at);

    private void Close(OpportunityStatus status, DateTimeOffset at)
    {
        if (!IsOpen)
        {
            throw new DomainException($"This opportunity is already {Status}.");
        }

        Status = status;
        ResolvedAt = at;
    }
}
