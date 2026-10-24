using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

/// <summary>How the purchase facts the engine used are supported.</summary>
public enum PurchaseEvidence
{
    None,
    SelfAsserted,
    Independent,
}

/// <summary>Everything the coverage engine reads about one asset — and nothing else, so a decision can be replayed from
/// its stored inputs.</summary>
public sealed record CoverageInputs(
    string CountryCode,
    BrandId? BrandId,
    CareCategory? Category,
    string? ModelCode,
    RetailerId? RetailerId,
    DateOnly? PurchasedOn,
    PurchaseEvidence PurchaseEvidence);

/// <summary>The engine's answer for one kind of cover, with the parts that made it (CARE/07 §18).</summary>
public sealed record CoverageFinding(
    CoverageType CoverageType,
    CoverageState State,
    CoverageRuleVersionId? RuleVersionId,
    RuleSpecificity? Specificity,
    RegistryConfidence? RuleConfidence,
    bool RuleStale,
    int ConflictingRules,
    DateOnly? StartsOn,
    DateOnly? ExpiresOn,
    bool RegistrationRequired,
    string? PlainLanguageSummary,
    SourceEvidenceId? SourceEvidenceId,
    string Explanation);

/// <summary>The whole evaluation: one finding per kind of cover a rule spoke to, and the headline state.</summary>
public sealed record CoverageEvaluation(CoverageState State, IReadOnlyList<CoverageFinding> Findings, string Explanation)
{
    /// <summary>The finding the headline came from, or null when nothing matched.</summary>
    public CoverageFinding? Primary => Findings.FirstOrDefault(f => f.State == State);
}

/// <summary>
/// The Coverage Engine (CARE/03 Module B, CARE/14 C4). Pure and deterministic: the same inputs, rules and date always
/// give the same answer, so a stored decision can be reproduced.
/// <list type="number">
/// <item>Only selectable rules (published or stale) for the asset's country, in force on the purchase date, whose
/// every stated condition matches the inputs, are candidates.</item>
/// <item>Per kind of cover, the most specific candidates win: model → brand + category → brand → retailer +
/// category. If the winners disagree, the answer is <see cref="CoverageState.NeedsReview"/> — the engine never picks
/// one.</item>
/// <item>Dates are claimed as fact (Active / Expiring / Expired) only on independent purchase evidence and a
/// high-confidence, current rule that starts at purchase and needs no registration. Otherwise the state is
/// <see cref="CoverageState.LikelyActive"/>, or <see cref="CoverageState.Unknown"/> where the dates have passed —
/// KADARI will not tell an owner their cover has ended on weak evidence.</item>
/// </list>
/// A warranty <c>ExpiresOn</c> is purchase date + duration (month-end clamped): cover runs until that day.
/// </summary>
public static class CoverageEvaluator
{
    /// <summary>Bumped whenever the rules above change, and stored on every decision.</summary>
    public const string EngineVersion = "coverage-1";

    /// <summary>How far ahead of expiry cover counts as expiring (CARE/03 §8: the 90-day reminder).</summary>
    public const int ExpiringWithinDays = 90;

    public static CoverageEvaluation Evaluate(
        CoverageInputs inputs, IEnumerable<CoverageRuleVersion> rules, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(rules);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var on = inputs.PurchasedOn ?? today;

        var candidates = rules
            .Where(r => r.IsSelectable && r.CountryCode == inputs.CountryCode && r.IsEffectiveOn(on) && Matches(r, inputs))
            .ToList();

        if (candidates.Count == 0)
        {
            return new CoverageEvaluation(
                CoverageState.Unknown, [],
                inputs.BrandId is null
                    ? "KADARI doesn't know the brand yet, so it can't look up the cover."
                    : "No verified rule covers this product yet.");
        }

        var findings = candidates
            .GroupBy(r => r.CoverageType)
            .OrderBy(g => g.Key)
            .Select(g => Decide(g.Key, g.ToList(), inputs, today, now))
            .ToList();

        var headline = findings.OrderBy(f => Rank(f.State)).First();
        return new CoverageEvaluation(headline.State, findings, headline.Explanation);
    }

    private static bool Matches(CoverageRuleVersion rule, CoverageInputs inputs) =>
        (rule.BrandId is null || rule.BrandId == inputs.BrandId)
        && (rule.Category is null || rule.Category == inputs.Category)
        && (rule.ModelCode is null || rule.ModelCode == CoverageRuleVersion.NormaliseModel(inputs.ModelCode))
        && (rule.RetailerId is null || rule.RetailerId == inputs.RetailerId);

    private static CoverageFinding Decide(
        CoverageType type, List<CoverageRuleVersion> candidates, CoverageInputs inputs, DateOnly today, DateTimeOffset now)
    {
        var top = candidates.Max(r => r.Specificity);
        var winners = candidates.Where(r => r.Specificity == top).ToList();
        var rule = winners[0];
        var conflicting = winners.Count(w => !SameOutcome(w, rule));

        if (conflicting > 0)
        {
            return new CoverageFinding(
                type, CoverageState.NeedsReview, null, top, null, false, conflicting + 1, null, null, false, null, null,
                "Two verified rules disagree about this product's cover, so a person will check it.");
        }

        var stale = winners.Any(w => w.IsStaleAt(now));

        if (inputs.PurchasedOn is not { } purchased)
        {
            return Finding(type, CoverageState.LikelyActive, rule, top, stale, null, null,
                "Likely warranty found. Add the purchase date so KADARI can work out the dates.");
        }

        var expires = purchased.AddMonths(rule.DurationMonths!.Value);
        var strong = inputs.PurchaseEvidence == PurchaseEvidence.Independent
                     && rule.Confidence >= RegistryConfidence.High
                     && !stale
                     && rule.StartBasis == CoverageStartBasis.PurchaseDate
                     && !rule.RegistrationRequired;

        if (!strong)
        {
            return today >= expires
                ? Finding(type, CoverageState.Unknown, rule, top, stale, purchased, expires,
                    "The cover may have ended, but KADARI can't confirm it yet. Add your proof of purchase.")
                : Finding(type, CoverageState.LikelyActive, rule, top, stale, purchased, expires,
                    WhyOnlyLikely(inputs, rule, stale));
        }

        if (today >= expires)
        {
            return Finding(type, CoverageState.Expired, rule, top, stale, purchased, expires,
                $"This cover ended on {expires:d MMMM yyyy}.");
        }

        return expires.DayNumber - today.DayNumber <= ExpiringWithinDays
            ? Finding(type, CoverageState.Expiring, rule, top, stale, purchased, expires,
                $"This cover ends on {expires:d MMMM yyyy}.")
            : Finding(type, CoverageState.Active, rule, top, stale, purchased, expires,
                $"Covered until {expires:d MMMM yyyy}.");
    }

    private static string WhyOnlyLikely(CoverageInputs inputs, CoverageRuleVersion rule, bool stale)
    {
        if (inputs.PurchaseEvidence != PurchaseEvidence.Independent)
        {
            return "Likely warranty found. Add your proof of purchase so KADARI can confirm the dates.";
        }

        if (rule.RegistrationRequired)
        {
            return "This cover depends on the product being registered with the manufacturer.";
        }

        if (rule.StartBasis != CoverageStartBasis.PurchaseDate)
        {
            return "This cover starts from delivery or registration, so the dates are approximate.";
        }

        return stale
            ? "Likely warranty found. KADARI is re-checking the manufacturer's terms."
            : "Likely warranty found. KADARI's source for this rule isn't strong enough to confirm it yet.";
    }

    private static CoverageFinding Finding(
        CoverageType type, CoverageState state, CoverageRuleVersion rule, RuleSpecificity specificity, bool stale,
        DateOnly? startsOn, DateOnly? expiresOn, string explanation) =>
        new(type, state, rule.Id, specificity, rule.Confidence, stale, 0, startsOn, expiresOn,
            rule.RegistrationRequired, rule.PlainLanguageSummary, rule.SourceEvidenceId, explanation);

    private static bool SameOutcome(CoverageRuleVersion a, CoverageRuleVersion b) =>
        a.DurationMonths == b.DurationMonths && a.StartBasis == b.StartBasis
        && a.RegistrationRequired == b.RegistrationRequired && a.ExtensionMonths == b.ExtensionMonths;

    /// <summary>Headline preference: the best cover the owner verifiably has, then the most useful uncertainty.</summary>
    private static int Rank(CoverageState state) => state switch
    {
        CoverageState.Active => 0,
        CoverageState.Expiring => 1,
        CoverageState.LikelyActive => 2,
        CoverageState.NeedsReview => 3,
        CoverageState.Expired => 4,
        CoverageState.VoidOrIneligible => 5,
        _ => 6,
    };
}
