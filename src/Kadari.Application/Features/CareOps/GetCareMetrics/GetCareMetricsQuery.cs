using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.GetCareMetrics;

/// <summary>
/// How KADARI Care is performing (CARE/14 C14 minimum metrics), counted from the operational tables themselves — no
/// separate analytics store, and nothing but aggregate counts and durations leaves this query: no customer, asset,
/// document or message content (CARE/15 G2). Brand-level counts and time to first external response need an analytics
/// pipeline and are not here yet.
/// </summary>
/// <remarks>Unscoped: an aggregate staff read across all customers, gated by <c>CareMetricsView</c> at the edge.</remarks>
public sealed record GetCareMetricsQuery : IQuery<CareMetricsDto>, IUnscopedRequest;

public sealed record CareMetricsDto(
    int CareActiveAssets,
    double CoverageFoundRate,
    IReadOnlyDictionary<string, int> CoverageByState,
    int RegistrationOpportunities,
    IReadOnlyDictionary<string, int> OpportunitiesByStatus,
    int RegistrationsStarted,
    int RegistrationsCompleted,
    double RegistrationCompletionRate,
    int CasesOpened,
    int CasesOpen,
    int SafetyFlaggedCases,
    IReadOnlyDictionary<string, int> CasesByRoute,
    IReadOnlyDictionary<string, int> OutcomeMix,
    double? MedianResolutionDays,
    double RejectionRate,
    int OpsWorkRaised,
    int OpsWorkOpen,
    double ManualInterventionsPer1000CareAssets,
    IReadOnlyDictionary<string, int> RemindersByStatus);

internal sealed class GetCareMetricsHandler(IKadariDbContext db) : IQueryHandler<GetCareMetricsQuery, CareMetricsDto>
{
    private static readonly CoverageState[] Found =
        [CoverageState.Active, CoverageState.Expiring, CoverageState.Expired, CoverageState.LikelyActive];

    public async Task<CareMetricsDto> Handle(GetCareMetricsQuery query, CancellationToken ct)
    {
        var coverage = await Count(db.CareProfiles.AsNoTracking().GroupBy(p => p.CoverageState), ct);
        var careAssets = coverage.Values.Sum();
        var found = coverage.Where(kv => Found.Contains(kv.Key)).Sum(kv => kv.Value);

        var opportunities = await Count(db.Set<RegistrationOpportunity>().AsNoTracking().GroupBy(o => o.Status), ct);
        var registrations = await Count(db.Set<CoverageRegistration>().AsNoTracking().GroupBy(r => r.Status), ct);
        var started = registrations.Values.Sum();
        var completed = registrations.GetValueOrDefault(RegistrationStatus.Completed);

        var cases = await db.Set<AssetCase>().AsNoTracking()
            .Select(c => new { c.Status, c.RouteType, c.Outcome, c.SafetyFlagged, c.OpenedAt, c.ClosedAt })
            .ToListAsync(ct);
        var closedWithOutcome = cases.Where(c => c.Outcome is not null).ToList();
        var durations = closedWithOutcome
            .Where(c => c.ClosedAt is not null)
            .Select(c => (c.ClosedAt!.Value - c.OpenedAt).TotalDays)
            .Order()
            .ToList();

        var ops = await db.Set<OpsWorkItem>().AsNoTracking().Select(w => w.Status).ToListAsync(ct);
        var reminders = await Count(db.Set<CareReminder>().AsNoTracking().GroupBy(r => r.Status), ct);

        return new CareMetricsDto(
            careAssets,
            Rate(found, careAssets),
            Names(coverage),
            opportunities.Values.Sum(),
            Names(opportunities),
            started,
            completed,
            Rate(completed, started),
            cases.Count,
            cases.Count(c => c.Status is not (CaseStatus.Resolved or CaseStatus.Rejected or CaseStatus.ClosedWithdrawn)),
            cases.Count(c => c.SafetyFlagged),
            cases.GroupBy(c => c.RouteType.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            closedWithOutcome.GroupBy(c => c.Outcome!.Value.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            durations.Count == 0 ? null : Math.Round(Median(durations), 1),
            Rate(closedWithOutcome.Count(c => c.Outcome == CaseOutcome.Rejected), closedWithOutcome.Count),
            ops.Count,
            ops.Count(s => s != OpsWorkStatus.Resolved),
            careAssets == 0 ? 0 : Math.Round(ops.Count * 1000.0 / careAssets, 1),
            Names(reminders));
    }

    private static async Task<Dictionary<TKey, int>> Count<TKey, TRow>(IQueryable<IGrouping<TKey, TRow>> groups, CancellationToken ct)
        where TKey : notnull =>
        await groups.Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    private static Dictionary<string, int> Names<TKey>(Dictionary<TKey, int> counts) where TKey : notnull =>
        counts.ToDictionary(kv => kv.Key.ToString()!, kv => kv.Value);

    private static double Rate(int part, int whole) => whole == 0 ? 0 : Math.Round((double)part / whole, 3);

    private static double Median(List<double> sorted) =>
        sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
}
