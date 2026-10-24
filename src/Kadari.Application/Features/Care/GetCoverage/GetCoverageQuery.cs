using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.GetCoverage;

/// <summary>
/// The coverage detail for an asset (CARE/05 C03, CARE/13 §4): each kind of cover KADARI found, its dates and
/// plain-language summary, and — under "How KADARI knows this" — the source, when it was last verified, and the
/// exact rule version and decision behind it. Customer-scoped; 404 when Care is not enabled.
/// </summary>
public sealed record GetCoverageQuery(Guid CustomerId, Guid AssetId) : IQuery<CoverageDetailDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record CoverageDetailDto(Guid AssetId, string CoverageState, IReadOnlyList<CoverageRecordDto> Records);

public sealed record CoverageRecordDto(
    string CoverageType,
    string State,
    DateOnly? StartsOn,
    DateOnly? ExpiresOn,
    bool RegistrationRequired,
    string? Summary,
    string Explanation,
    CoverageProvenanceDto? HowKadariKnows);

public sealed record CoverageProvenanceDto(
    string SourceTitle,
    string? SourceUrl,
    DateTimeOffset LastVerifiedAt,
    string Confidence,
    Guid RuleVersionId,
    int RuleVersion,
    string RuleStatus,
    Guid DecisionId,
    DateTimeOffset DecidedAt);

internal sealed class GetCoverageHandler(IKadariDbContext db, OwnedAssets assets)
    : IQueryHandler<GetCoverageQuery, CoverageDetailDto>
{
    public async Task<CoverageDetailDto> Handle(GetCoverageQuery query, CancellationToken ct)
    {
        var asset = await assets.GetAsync(query.CustomerId, query.AssetId, ct);
        var profile = await db.CareProfiles.AsNoTracking().Include(p => p.Coverage)
            .FirstOrDefaultAsync(p => p.AssetId == asset.Id, ct)
            ?? throw new NotFoundException(nameof(CareProfile), query.AssetId);

        var ruleIds = profile.Coverage.Select(r => r.RuleVersionId).OfType<CoverageRuleVersionId>().ToList();
        var rules = await db.Set<CoverageRuleVersion>().AsNoTracking()
            .Where(r => ruleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var sourceIds = rules.Values.Select(r => r.SourceEvidenceId).Distinct().ToList();
        var sources = await db.Set<SourceEvidence>().AsNoTracking()
            .Where(s => sourceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var decisionIds = profile.Coverage.Select(r => r.DecisionId).Distinct().ToList();
        var decisions = await db.Set<CoverageDecision>().AsNoTracking()
            .Where(d => decisionIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.EvaluatedAt, ct);

        var records = profile.Coverage
            .OrderBy(r => r.CoverageType)
            .Select(r => new CoverageRecordDto(
                r.CoverageType.ToString(), r.State.ToString(), r.StartsOn, r.ExpiresOn, r.RegistrationRequired,
                r.PlainLanguageSummary, r.Explanation,
                r.RuleVersionId is { } ruleId && rules.TryGetValue(ruleId, out var rule) && sources.TryGetValue(rule.SourceEvidenceId, out var source)
                    ? new CoverageProvenanceDto(
                        source.Title, source.Url, rule.LastVerifiedAt, rule.Confidence.ToString(), rule.Id.Value, rule.Version,
                        rule.Status.ToString(), r.DecisionId.Value, decisions.GetValueOrDefault(r.DecisionId))
                    : null))
            .ToList();

        return new CoverageDetailDto(asset.Id.Value, profile.CoverageState.ToString(), records);
    }
}
