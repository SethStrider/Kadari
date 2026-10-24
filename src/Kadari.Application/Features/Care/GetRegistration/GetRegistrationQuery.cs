using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Care.GetRegistration;

/// <summary>
/// Extra cover for an asset (CARE/05 C04–C06): each open opportunity — the benefit, the deadline, who receives the
/// data and exactly which data, and the wording the owner would agree to — and every registration with its status.
/// Customer-scoped.
/// </summary>
public sealed record GetRegistrationQuery(Guid CustomerId, Guid AssetId) : IQuery<RegistrationOverviewDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record RegistrationOverviewDto(
    Guid AssetId, IReadOnlyList<OpportunityDto> Opportunities, IReadOnlyList<RegistrationDto> Registrations);

public sealed record OpportunityDto(
    Guid OpportunityId,
    string Benefit,
    DateOnly? DeadlineOn,
    string Mode,
    string Provider,
    string DestinationType,
    string Destination,
    IReadOnlyList<SharedFieldDto> Shared,
    string TermsVersion,
    string Terms);

internal sealed class GetRegistrationHandler(IKadariDbContext db, OwnedAssets assets)
    : IQueryHandler<GetRegistrationQuery, RegistrationOverviewDto>
{
    public async Task<RegistrationOverviewDto> Handle(GetRegistrationQuery query, CancellationToken ct)
    {
        var asset = await assets.GetAsync(query.CustomerId, query.AssetId, ct);
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == new CustomerId(query.CustomerId), ct);

        var opportunities = await db.Set<RegistrationOpportunity>().AsNoTracking()
            .Where(o => o.AssetId == asset.Id && o.Status == OpportunityStatus.Available)
            .ToListAsync(ct);
        var registrations = await db.Set<CoverageRegistration>().AsNoTracking()
            .Where(r => r.AssetId == asset.Id)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);

        var ruleIds = opportunities.Select(o => o.RuleVersionId).Concat(registrations.Select(r => r.RuleVersionId)).Distinct().ToList();
        var rules = await db.Set<RegistrationRuleVersion>().AsNoTracking().Where(r => ruleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var brandIds = rules.Values.Select(r => r.BrandId).Distinct().ToList();
        var brands = await db.Set<Brand>().AsNoTracking().Where(b => brandIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var authIds = registrations.Select(r => r.AuthorisationId).ToList();
        var scopes = await db.Set<ExternalActionAuthorisation>().AsNoTracking()
            .Where(a => authIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.ScopeJson, ct);

        return new RegistrationOverviewDto(
            asset.Id.Value,
            opportunities
                .Where(o => rules.ContainsKey(o.RuleVersionId))
                .Select(o =>
                {
                    var rule = rules[o.RuleVersionId];
                    return new OpportunityDto(
                        o.Id.Value, o.BenefitSummary, o.DeadlineOn, ExecutedMode(rule.Mode).ToString(),
                        brands.GetValueOrDefault(rule.BrandId) ?? "The manufacturer", rule.DestinationType.ToString(), rule.Destination,
                        RegistrationPack.For(rule, customer, asset), RegistrationPack.TermsVersion, RegistrationPack.Terms);
                })
                .ToList(),
            registrations
                .Select(r => new RegistrationDto(
                    r.Id.Value, r.Status.ToString(), r.Mode.ToString(),
                    rules.TryGetValue(r.RuleVersionId, out var rule) ? rule.DestinationType.ToString() : "WebPage",
                    r.Destination, rules.GetValueOrDefault(r.RuleVersionId)?.Instructions, r.StartedAt, r.ExternalReference,
                    r.RegisteredOn, r.FailureReason, SharedFrom(scopes.GetValueOrDefault(r.AuthorisationId))))
                .ToList());
    }

    private static RegistrationMode ExecutedMode(RegistrationMode mode) =>
        mode == RegistrationMode.Automatic ? RegistrationMode.Assisted : mode;

    /// <summary>What the owner authorised to be shared, as recorded on the authorisation.</summary>
    internal static IReadOnlyList<SharedFieldDto> SharedFrom(string? scopeJson)
    {
        if (string.IsNullOrWhiteSpace(scopeJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<AuthorisedScope>(scopeJson, ScopeJson.Options)?.Shared ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>The shape recorded on an authorisation: where the data goes and exactly what was shown and agreed.</summary>
internal sealed record AuthorisedScope(string Destination, IReadOnlyList<SharedFieldDto> Shared);

internal static class ScopeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
