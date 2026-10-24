using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.Services;

/// <summary>
/// Runs the Routing Engine for a case and records the result (CARE/14 C8): the inputs come from the asset's own purchase
/// facts (brand, model, retailer, matched against the registry), the Care profile's category and current cover, and the
/// issue class the owner chose. The decision is stored append-only with its inputs, then applied to the case together
/// with the matched rule's counterparty. The one place a route is decided.
/// </summary>
internal sealed class CareRouting(IKadariDbContext db, RegistryLookup registry, IOptions<CareOptions> options, TimeProvider clock)
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public async Task<RoutingDecision> RouteAsync(Asset asset, CareProfile profile, AssetCase @case, CancellationToken ct)
    {
        var country = options.Value.CountryCode.Trim().ToUpperInvariant();
        var facts = asset.LatestPurchase?.Purchase;
        var brand = await registry.BrandAsync(facts?.Brand, ct);
        var retailer = await registry.RetailerAsync(facts?.Retailer, country, ct);

        var inputs = new RoutingInputs(
            country, brand?.Id, profile.Category, facts?.Model, retailer?.Id, @case.CoverageAtOpen, @case.IssueClass);

        var brandId = brand?.Id;
        var retailerId = retailer?.Id;
        var rules = await db.Set<RoutingRuleVersion>().AsNoTracking()
            .Where(r => r.CountryCode == country
                        && (r.Status == RuleStatus.Published || r.Status == RuleStatus.Stale)
                        && (r.BrandId == null || r.BrandId == brandId)
                        && (r.RetailerId == null || r.RetailerId == retailerId))
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        var result = RoutingEvaluator.Evaluate(inputs, rules, now);
        var decision = RoutingDecision.Record(asset.Id, result, Snapshot(inputs, facts, brand, retailer, result), now);
        db.Set<RoutingDecision>().Add(decision);

        var rule = result.RuleVersionId is { } id ? rules.First(r => r.Id == id) : null;
        @case.ApplyRoute(decision, rule, now);
        return decision;
    }

    private static string Snapshot(RoutingInputs inputs, PurchaseFacts? facts, Brand? brand, Retailer? retailer, RoutingResult result) =>
        JsonSerializer.Serialize(new
        {
            inputs = new
            {
                inputs.CountryCode,
                brandId = inputs.BrandId?.Value,
                brandText = facts?.Brand,
                brandName = brand?.Name,
                category = inputs.Category?.ToString(),
                inputs.ModelCode,
                retailerId = inputs.RetailerId?.Value,
                retailerText = facts?.Retailer,
                retailerName = retailer?.Name,
                coverage = inputs.Coverage.ToString(),
                issueClass = inputs.IssueClass.ToString(),
            },
            result = new { routeType = result.RouteType.ToString(), confidence = result.Confidence.ToString(), result.CompetingRoutes },
        }, SnapshotJson);
}
