using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

/// <summary>How sure the routing engine is (CARE/07 §9): green proceeds, amber asks the owner to confirm, red goes to a
/// person.</summary>
public enum RouteConfidence
{
    Green,
    Amber,
    Red,
}

/// <summary>Everything routing reads — and nothing else, so a decision can be replayed from its stored inputs.</summary>
public sealed record RoutingInputs(
    string CountryCode,
    BrandId? BrandId,
    CareCategory? Category,
    string? ModelCode,
    RetailerId? RetailerId,
    CoverageState Coverage,
    IssueClass IssueClass);

public sealed record RoutingResult(
    RouteType RouteType,
    RouteConfidence Confidence,
    RoutingRuleVersionId? RuleVersionId,
    string Explanation,
    int CompetingRoutes);

/// <summary>
/// The Routing Engine (CARE/03 Module F, CARE/14 C8). Pure and deterministic. A rule is a candidate when it is
/// selectable for the country and every condition it states matches — brand, category, model, retailer, cover (in or
/// out) and issue class. The most specific candidates win (model → brand + category → brand → retailer + category),
/// then the lowest priority number. If the winners still point to different routes, or nothing matches, the answer is
/// <see cref="RouteType.ResearchRequired"/> at <see cref="RouteConfidence.Red"/>: a person decides; KADARI never picks
/// (CARE/07 §9 RED, CARE/19 rule 6). A single winner is green when it is confident, current and — if it depends on cover — the cover is settled;
/// otherwise amber, for the owner to confirm.
/// A rule for products in (or out of) cover never matches while the cover is unknown or needs review.
/// </summary>
public static class RoutingEvaluator
{
    public const string EngineVersion = "routing-1";

    public static RoutingResult Evaluate(RoutingInputs inputs, IEnumerable<RoutingRuleVersion> rules, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var inCover = inputs.Coverage is CoverageState.Active or CoverageState.Expiring or CoverageState.LikelyActive;
        var coverKnown = inputs.Coverage is not (CoverageState.Unknown or CoverageState.NeedsReview);

        var candidates = rules
            .Where(r => r.IsSelectable && r.CountryCode == inputs.CountryCode && Matches(r, inputs, inCover, coverKnown))
            .ToList();

        if (candidates.Count == 0)
        {
            return new RoutingResult(RouteType.ResearchRequired, RouteConfidence.Red, null,
                "KADARI doesn't have a verified route for this product yet. A person will find the right place.", 0);
        }

        var top = candidates.Max(r => r.Specificity);
        var mostSpecific = candidates.Where(r => r.Specificity == top).ToList();
        var bestPriority = mostSpecific.Min(r => r.Priority);
        var winners = mostSpecific.Where(r => r.Priority == bestPriority).ToList();

        if (winners.Select(r => r.RouteType).Distinct().Count() > 1)
        {
            return new RoutingResult(RouteType.ResearchRequired, RouteConfidence.Red, null,
                "More than one route could apply. A person will confirm the right one.", winners.Count);
        }

        var rule = winners.OrderBy(r => r.LogicalRuleId).First();
        // A route that depends on cover is only as sure as the cover: "likely in warranty" makes it amber.
        var coverSettled = rule.Coverage == RouteCoverage.Any
            || inputs.Coverage is CoverageState.Active or CoverageState.Expiring or CoverageState.Expired or CoverageState.VoidOrIneligible;
        var green = rule.Confidence >= RegistryConfidence.High && !rule.IsStaleAt(now) && coverSettled;
        return new RoutingResult(rule.RouteType, green ? RouteConfidence.Green : RouteConfidence.Amber, rule.Id,
            green ? "Verified route for this product." : "Likely route — check it suits you before going ahead.",
            winners.Count - 1);
    }

    private static bool Matches(RoutingRuleVersion rule, RoutingInputs inputs, bool inCover, bool coverKnown) =>
        (rule.BrandId is null || rule.BrandId == inputs.BrandId)
        && (rule.Category is null || rule.Category == inputs.Category)
        && (rule.ModelCode is null || rule.ModelCode == CoverageRuleVersion.NormaliseModel(inputs.ModelCode))
        && (rule.RetailerId is null || rule.RetailerId == inputs.RetailerId)
        && rule.Coverage switch
        {
            RouteCoverage.InCover => coverKnown && inCover,
            RouteCoverage.OutOfCover => coverKnown && !inCover,
            _ => true,
        }
        && (rule.IssueClass == IssueClass.Any || rule.IssueClass == inputs.IssueClass);
}

/// <summary>
/// The safety gate (CARE/06 §11, CARE/05 C10). Plain keyword signals plus the owner's own "this is unsafe" answer —
/// deliberately simple and conservative: a false alarm costs a moment, a missed one can cost a home. When it trips, the
/// owner sees stop-use guidance before anything else, and the case is kept for afterwards.
/// </summary>
public static class SafetyGate
{
    /// <summary>
    /// The guidance shown when the gate trips. Kept generic on purpose — it never suggests diagnosis or repair.
    /// PROVISIONAL: this wording must be reviewed and signed off before release (CARE/15 G2 "verified safe wording").
    /// </summary>
    public const string Guidance =
        "Stop using it now. If it is safe to do so, switch it off at the wall or unplug it, and keep people and pets away. " +
        "Do not open it or try to fix it. If there is fire, smoke or a smell of gas, leave the area and call emergency " +
        "services on 112 from a mobile phone. KADARI has saved your report — come back when it is safe.";

    private static readonly string[] Signals =
    [
        "smoke", "smoking", "fire", "flame", "burning smell", "smells burnt", "smell of burning", "sparks", "sparking",
        "gas smell", "smell of gas", "smells of gas", "gas leak", "exposed wire", "bare wire", "electric shock", "shocked me",
        "shocks me", "overheating", "too hot to touch", "melting", "melted", "swollen battery", "battery is swollen",
        "battery swelling", "bulging battery", "explod", "leaking acid",
    ];

    public static bool Trips(string description, bool? ownerSaysUnsafe)
    {
        if (ownerSaysUnsafe == true)
        {
            return true;
        }

        var text = description.ToLowerInvariant();
        return Signals.Any(text.Contains);
    }
}
