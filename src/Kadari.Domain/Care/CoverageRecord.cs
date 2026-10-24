using Kadari.Domain.Abstractions;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

public readonly record struct CoverageRecordId(Guid Value)
{
    public static CoverageRecordId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// One kind of cover an asset currently has, as the latest decision found it (CARE/12 §2 CoverageRecord) — a
/// manufacturer warranty, a retailer guarantee, an extended plan. An asset may hold several at once. A child of
/// <see cref="CareProfile"/>; it always names the decision and rule version that produced it, so the card can say
/// "How KADARI knows this".
/// </summary>
public sealed class CoverageRecord : Entity<CoverageRecordId>
{
    private CoverageRecord() { }   // EF

    private CoverageRecord(CoverageRecordId id, CoverageType coverageType) : base(id) => CoverageType = coverageType;

    public CoverageType CoverageType { get; private set; }
    public CoverageState State { get; private set; }
    public CoverageRuleVersionId? RuleVersionId { get; private set; }
    public DateOnly? StartsOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public bool RegistrationRequired { get; private set; }
    public RegistryConfidence? Confidence { get; private set; }
    public string? PlainLanguageSummary { get; private set; }
    public SourceEvidenceId? SourceEvidenceId { get; private set; }
    public string Explanation { get; private set; } = null!;
    public CoverageDecisionId DecisionId { get; private set; }

    internal static CoverageRecord From(CoverageFinding finding, CoverageDecisionId decisionId)
    {
        var record = new CoverageRecord(CoverageRecordId.New(), finding.CoverageType);
        record.Apply(finding, decisionId);
        return record;
    }

    internal void Apply(CoverageFinding finding, CoverageDecisionId decisionId)
    {
        State = finding.State;
        RuleVersionId = finding.RuleVersionId;
        StartsOn = finding.StartsOn;
        ExpiresOn = finding.ExpiresOn;
        RegistrationRequired = finding.RegistrationRequired;
        Confidence = finding.RuleConfidence;
        PlainLanguageSummary = finding.PlainLanguageSummary;
        SourceEvidenceId = finding.SourceEvidenceId;
        Explanation = finding.Explanation;
        DecisionId = decisionId;
    }
}
