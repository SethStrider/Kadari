using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

public readonly record struct RoutingDecisionId(Guid Value)
{
    public static RoutingDecisionId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// Why KADARI pointed a problem where it did, at one moment (CARE/12 §5 RoutingDecision): the inputs, the rule
/// version that matched (if any), the route, the confidence and the engine version. Append-only and immutable, like
/// <see cref="CoverageDecision"/>. A person who corrects a route records a new decision with
/// <see cref="HumanOverride"/> and a reason; the engine's decision stays as it was.
/// </summary>
public sealed class RoutingDecision
{
    public const int MaxExplanationLength = 500;

    private RoutingDecision() { }   // EF

    public RoutingDecisionId Id { get; private init; }
    public AssetId AssetId { get; private init; }
    public DateTimeOffset EvaluatedAt { get; private init; }

    /// <summary>The <see cref="RoutingInputs"/>, serialised by the caller.</summary>
    public string InputSnapshotJson { get; private init; } = null!;

    public RoutingRuleVersionId? MatchedRuleVersionId { get; private init; }
    public RouteType RouteType { get; private init; }
    public RouteConfidence Confidence { get; private init; }
    public string Explanation { get; private init; } = null!;
    public string EngineVersion { get; private init; } = null!;
    public bool HumanOverride { get; private init; }
    public string? HumanOverrideReason { get; private init; }

    public static RoutingDecision Record(AssetId assetId, RoutingResult result, string inputSnapshotJson, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSnapshotJson);
        return new RoutingDecision
        {
            Id = RoutingDecisionId.New(),
            AssetId = assetId,
            EvaluatedAt = at,
            InputSnapshotJson = inputSnapshotJson,
            MatchedRuleVersionId = result.RuleVersionId,
            RouteType = result.RouteType,
            Confidence = result.Confidence,
            Explanation = Clip(result.Explanation),
            EngineVersion = RoutingEvaluator.EngineVersion,
        };
    }

    /// <summary>A person's correction (Care Ops). It never claims more than a person can know: the route is theirs,
    /// marked as an override, and points at no engine rule.</summary>
    public static RoutingDecision Override(
        AssetId assetId, RouteType routeType, string reason, string inputSnapshotJson, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSnapshotJson);
        if (routeType == RouteType.ResearchRequired)
        {
            throw new DomainException("An override names the route a person found.");
        }

        return new RoutingDecision
        {
            Id = RoutingDecisionId.New(),
            AssetId = assetId,
            EvaluatedAt = at,
            InputSnapshotJson = inputSnapshotJson,
            RouteType = routeType,
            Confidence = RouteConfidence.Green,
            Explanation = "Route confirmed by a KADARI Care specialist.",
            EngineVersion = RoutingEvaluator.EngineVersion,
            HumanOverride = true,
            HumanOverrideReason = Clip(reason.Trim()),
        };
    }

    private static string Clip(string text) => text.Length > MaxExplanationLength ? text[..MaxExplanationLength] : text;
}
