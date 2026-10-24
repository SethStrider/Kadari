using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Audit;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.Services;

/// <summary>
/// Runs the Coverage Engine for one asset and records the result (CARE/14 C4): gathers the inputs from the asset's
/// own evidence (the latest purchase facts, whether a receipt backs them) and the registry (brand and retailer by
/// name, the selectable rules for the country), evaluates, stores an append-only <see cref="CoverageDecision"/> with
/// the exact inputs and rule versions, applies it to the <see cref="CareProfile"/> and audits it. The one place a
/// coverage decision is made — shared by enabling Care, the explicit re-evaluation and the evidence reaction.
/// </summary>
internal sealed class CareCoverage(
    IKadariDbContext db, ICurrentUser user, CareReminders reminders, RegistryLookup registry, IOptions<CareOptions> options, TimeProvider clock)
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public async Task<CoverageDecision> EvaluateAsync(Asset asset, CareProfile profile, CancellationToken ct)
    {
        var country = options.Value.CountryCode.Trim().ToUpperInvariant();
        var purchase = asset.LatestPurchase;
        var facts = purchase?.Purchase;

        var brand = await registry.BrandAsync(facts?.Brand, ct);
        var retailer = await registry.RetailerAsync(facts?.Retailer, country, ct);

        var registeredOn = await db.Set<CoverageRegistration>().AsNoTracking()
            .Where(r => r.AssetId == asset.Id && r.Status == RegistrationStatus.Completed)
            .OrderBy(r => r.RegisteredOn)
            .Select(r => r.RegisteredOn)
            .FirstOrDefaultAsync(ct);

        var inputs = new CoverageInputs(
            country, brand?.Id, profile.Category, facts?.Model, retailer?.Id, facts?.PurchasedOn, EvidenceFor(asset, purchase),
            registeredOn);

        var brandId = brand?.Id;
        var rules = await db.Set<CoverageRuleVersion>().AsNoTracking()
            .Where(r => r.CountryCode == country
                        && (r.Status == RuleStatus.Published || r.Status == RuleStatus.Stale)
                        && (r.BrandId == null || r.BrandId == brandId))
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        var evaluation = CoverageEvaluator.Evaluate(inputs, rules, now);
        var decision = CoverageDecision.Record(asset.Id, evaluation, Snapshot(inputs, facts, brand, retailer, evaluation), now);

        db.Set<CoverageDecision>().Add(decision);
        var previous = profile.CoverageState;
        profile.ApplyEvaluation(evaluation, decision, now);

        var openOffers = await RefreshOpportunitiesAsync(asset, inputs, now, ct);
        await reminders.SyncAsync(profile, openOffers, ct);

        db.Set<AuditEvent>().Add(AuditEvent.Record(
            user.IsAuthenticated ? ActorKind.Account : ActorKind.System, "Asset", asset.Id.Value, "CareCoverageEvaluated",
            now, actorId: user.AccountId, previousState: previous.ToString(), newState: evaluation.State.ToString()));

        return decision;
    }

    /// <summary>
    /// "Extra cover available" (CARE/05 C04): for every published registration rule that applies to this product, keep
    /// one opportunity per asset and rule — surfaced when first seen, refreshed to the newest version and purchase date,
    /// never offered again once the owner dismissed or took it, and not offered at all when the asset is already
    /// registered under that rule. An opportunity past its deadline expires.
    /// </summary>
    private async Task<IReadOnlyList<RegistrationOpportunity>> RefreshOpportunitiesAsync(
        Asset asset, CoverageInputs inputs, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.Set<RegistrationOpportunity>()
            .Where(o => o.AssetId == asset.Id)
            .ToDictionaryAsync(o => o.LogicalRuleId, ct);

        if (inputs.BrandId is not { } brandId)
        {
            return existing.Values.Where(o => o.IsOpen).ToList();
        }

        var rules = (await db.Set<RegistrationRuleVersion>().AsNoTracking()
                .Where(r => r.BrandId == brandId && r.CountryCode == inputs.CountryCode
                            && (r.Status == RuleStatus.Published || r.Status == RuleStatus.Stale))
                .ToListAsync(ct))
            .Where(r => r.AppliesTo(inputs.CountryCode, inputs.BrandId, inputs.Category, inputs.ModelCode))
            .GroupBy(r => r.LogicalRuleId)
            .Select(g => g.OrderByDescending(r => r.Version).First())
            .ToList();

        foreach (var rule in rules)
        {
            if (existing.TryGetValue(rule.LogicalRuleId, out var opportunity))
            {
                opportunity.Refresh(rule, inputs.PurchasedOn, now);
            }
            else if (inputs.RegisteredOn is null)
            {
                var surfaced = RegistrationOpportunity.Surface(asset.Id, rule, inputs.PurchasedOn, now);
                db.Set<RegistrationOpportunity>().Add(surfaced);
                existing[rule.LogicalRuleId] = surfaced;
            }
        }

        return existing.Values.Where(o => o.IsOpen).ToList();
    }

    /// <summary>
    /// Purchase facts are independent when they cite a stored receipt, or when the asset holds one — the owner read
    /// the date off a receipt KADARI keeps. Typed facts with no receipt anywhere are self-asserted.
    /// </summary>
    private static PurchaseEvidence EvidenceFor(Asset asset, AssetEvidence? purchase)
    {
        if (purchase is null)
        {
            return PurchaseEvidence.None;
        }

        var receiptHeld = asset.Evidence.Any(e =>
            e.Kind == AssetEvidenceKind.Ownership && e.Strength == EvidenceStrength.Independent && e.Source == "receipt");

        return purchase.Strength == EvidenceStrength.Independent || receiptHeld
            ? PurchaseEvidence.Independent
            : PurchaseEvidence.SelfAsserted;
    }

    private static string Snapshot(
        CoverageInputs inputs, PurchaseFacts? facts, Brand? brand, Retailer? retailer, CoverageEvaluation evaluation) =>
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
                inputs.PurchasedOn,
                purchaseEvidence = inputs.PurchaseEvidence.ToString(),
                inputs.RegisteredOn,
            },
            findings = evaluation.Findings.Select(f => new
            {
                coverageType = f.CoverageType.ToString(),
                state = f.State.ToString(),
                ruleVersionId = f.RuleVersionId?.Value,
                specificity = f.Specificity?.ToString(),
                ruleConfidence = f.RuleConfidence?.ToString(),
                f.RuleStale,
                f.ConflictingRules,
                f.StartsOn,
                f.ExpiresOn,
            }),
        }, SnapshotJson);
}
