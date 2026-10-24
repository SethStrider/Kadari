using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.CareOps.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.CareOps.GetOpsWorkItem;

/// <summary>What an operator needs for one task (CARE/11 §6, §22): the product facts, the cover, the case's route and
/// timeline, the routing models to choose from. The owner's own description is shown only for a safety review — a route
/// is found from the product, not from what the owner wrote. No contact details.</summary>
/// <remarks>Unscoped: a staff surface across all customers, gated by <c>CareOpsReview</c> at the edge.</remarks>
public sealed record GetOpsWorkItemQuery(Guid WorkItemId) : IQuery<OpsWorkItemDetailDto>, IUnscopedRequest;

public sealed record OpsCaseContextDto(
    Guid CaseId,
    string Status,
    string IssueClass,
    string CoverageAtOpen,
    string RouteType,
    string RouteConfidence,
    bool SafetyFlagged,
    string? IssueSummary,
    IReadOnlyList<string> Timeline);

public sealed record OpsProductContextDto(string? Category, string? Brand, string? Model, string? Retailer, DateOnly? PurchasedOn, string? CoverageState);

public sealed record OpsRoutingModelDto(string Code, string Name, string? DefaultAction);

public sealed record OpsWorkItemDetailDto(
    OpsWorkItemDto Item,
    OpsProductContextDto? Product,
    OpsCaseContextDto? Case,
    IReadOnlyList<OpsRoutingModelDto> RoutingModels);

internal sealed class GetOpsWorkItemHandler(IKadariDbContext db, TimeProvider clock) : IQueryHandler<GetOpsWorkItemQuery, OpsWorkItemDetailDto>
{
    public async Task<OpsWorkItemDetailDto> Handle(GetOpsWorkItemQuery query, CancellationToken ct)
    {
        var item = await db.Set<OpsWorkItem>().AsNoTracking().FirstOrDefaultAsync(w => w.Id == new OpsWorkItemId(query.WorkItemId), ct)
            ?? throw new NotFoundException(nameof(OpsWorkItem), query.WorkItemId);

        OpsProductContextDto? product = null;
        if (item.AssetId is { } assetId)
        {
            var asset = await db.Assets.AsNoTracking().Include(a => a.Evidence).FirstOrDefaultAsync(a => a.Id == assetId, ct);
            var profile = await db.CareProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AssetId == assetId, ct);
            var facts = asset?.LatestPurchase?.Purchase;
            product = new OpsProductContextDto(profile?.Category.ToString(), facts?.Brand, facts?.Model, facts?.Retailer,
                facts?.PurchasedOn, profile?.CoverageState.ToString());
        }

        OpsCaseContextDto? caseContext = null;
        if (item.CaseId is { } caseId && await db.Set<AssetCase>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == caseId, ct) is { } c)
        {
            caseContext = new OpsCaseContextDto(c.Id.Value, c.Status.ToString(), c.IssueClass.ToString(), c.CoverageAtOpen.ToString(),
                c.RouteType.ToString(), c.RouteConfidence.ToString(), c.SafetyFlagged,
                item.WorkType == OpsWorkType.SafetyReview ? c.IssueSummary : null,
                c.Events.OrderBy(e => e.Sequence).Select(e => $"{e.OccurredAt:yyyy-MM-dd HH:mm} {e.Summary}").ToList());
        }

        var models = await db.Set<RoutingModel>().AsNoTracking().OrderBy(m => m.Code)
            .Select(m => new OpsRoutingModelDto(m.Code, m.Name, m.DefaultAction)).ToListAsync(ct);

        return new OpsWorkItemDetailDto(OpsWorkItemDto.From(item, clock.GetUtcNow()), product, caseContext, models);
    }
}
