using Kadari.Domain.Assets;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

public readonly record struct CoverageDecisionId(Guid Value)
{
    public static CoverageDecisionId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// Why KADARI said what it said about an asset's cover, at one moment (CARE/12 §5): the exact inputs, the exact
/// rule versions that matched, the result and the engine version. Append-only and immutable — never updated, never
/// soft-deleted — so a later rule change or a dispute can always be answered from the record (CARE/15 G0).
/// </summary>
public sealed class CoverageDecision
{
    public const int MaxExplanationLength = 500;

    private CoverageDecision() { }   // EF

    public CoverageDecisionId Id { get; private init; }
    public AssetId AssetId { get; private init; }
    public DateTimeOffset EvaluatedAt { get; private init; }
    public CoverageState ResultState { get; private init; }

    /// <summary>The <see cref="CoverageInputs"/> and per-finding components, serialised by the caller (the domain
    /// stays free of serialisers).</summary>
    public string InputSnapshotJson { get; private init; } = null!;

    public Guid[] MatchedRuleVersionIds { get; private init; } = [];
    public string Explanation { get; private init; } = null!;
    public string EngineVersion { get; private init; } = null!;

    public static CoverageDecision Record(
        AssetId assetId, CoverageEvaluation evaluation, string inputSnapshotJson, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSnapshotJson);
        var explanation = evaluation.Explanation.Length > MaxExplanationLength
            ? evaluation.Explanation[..MaxExplanationLength]
            : evaluation.Explanation;

        return new CoverageDecision
        {
            Id = CoverageDecisionId.New(),
            AssetId = assetId,
            EvaluatedAt = evaluatedAt,
            ResultState = evaluation.State,
            InputSnapshotJson = inputSnapshotJson,
            MatchedRuleVersionIds = evaluation.Findings
                .Select(f => f.RuleVersionId)
                .OfType<CoverageRuleVersionId>()
                .Select(id => id.Value)
                .ToArray(),
            Explanation = explanation,
            EngineVersion = CoverageEvaluator.EngineVersion,
        };
    }
}
