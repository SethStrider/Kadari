using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;

namespace Kadari.Application.Features.Care.GetCareSummary;

/// <summary>
/// The Care card and overview for one asset (CARE/13 §2, CARE/05 C01–C02): the card state with its plain-language
/// headline and one primary action, the coverage position, the next date that matters, open cases, what evidence is
/// missing, and the purchase facts KADARI is working from. Works before Care is enabled (the card then invites the
/// owner to start). Customer-scoped.
/// </summary>
public sealed record GetCareSummaryQuery(Guid CustomerId, Guid AssetId) : IQuery<CareSummaryDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record CareSummaryDto(
    Guid AssetId,
    bool Enabled,
    string? Category,
    string CardState,
    string Headline,
    string Detail,
    string PrimaryAction,
    string? CoverageState,
    DateOnly? CoverageExpiresOn,
    DateOnly? NextAttentionOn,
    int OpenCaseCount,
    IReadOnlyList<string> EvidenceGaps,
    PurchaseDetailsDto? Purchase,
    string? SerialNumber,
    DateTimeOffset? LastEvaluatedAt);

public sealed record PurchaseDetailsDto(
    string? Brand, string? Model, string? Retailer, DateOnly? PurchasedOn, bool BackedByReceipt, DateTimeOffset RecordedAt);

internal sealed class GetCareSummaryHandler(IKadariDbContext db, OwnedAssets assets)
    : IQueryHandler<GetCareSummaryQuery, CareSummaryDto>
{
    public async Task<CareSummaryDto> Handle(GetCareSummaryQuery query, CancellationToken ct)
    {
        var asset = await assets.GetAsync(query.CustomerId, query.AssetId, ct);
        var profile = await db.CareProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AssetId == asset.Id, ct);

        var offer = profile is null
            ? null
            : await db.Set<Domain.Care.RegistrationOpportunity>().AsNoTracking()
                .Where(o => o.AssetId == asset.Id && o.Status == Domain.Care.OpportunityStatus.Available)
                .OrderBy(o => o.DeadlineOn)
                .FirstOrDefaultAsync(ct);
        var registration = profile is null
            ? null
            : await db.Set<Domain.Care.CoverageRegistration>().AsNoTracking()
                .Where(r => r.AssetId == asset.Id
                            && (r.Status == Domain.Care.RegistrationStatus.WaitingUserStep
                                || r.Status == Domain.Care.RegistrationStatus.FailedRetryable))
                .FirstOrDefaultAsync(ct);

        var card = CareCard.For(asset, profile, offer, registration);
        var purchase = asset.LatestPurchase;

        return new CareSummaryDto(
            asset.Id.Value,
            profile is not null,
            profile?.Category.ToString(),
            card.State.ToString(),
            card.Headline,
            card.Detail,
            card.PrimaryAction.ToString(),
            profile?.CoverageState.ToString(),
            profile?.CoverageExpiresOn,
            profile?.NextAttentionOn,
            profile?.OpenCaseCount ?? 0,
            card.Gaps.Select(g => g.ToString()).ToList(),
            purchase?.Purchase is { } facts
                ? new PurchaseDetailsDto(facts.Brand, facts.Model, facts.Retailer, facts.PurchasedOn,
                    purchase.Strength == Domain.Assets.EvidenceStrength.Independent, purchase.AssertedAt)
                : null,
            AssetFacts.SerialOf(asset),
            profile?.LastEvaluatedAt);
    }
}
