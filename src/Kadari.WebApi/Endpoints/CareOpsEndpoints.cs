using MediatR;
using Kadari.Application.Features.CareOps.GetCareMetrics;
using Kadari.Application.Features.CareOps.GetOpsQueue;
using Kadari.Application.Features.CareOps.GetOpsWorkItem;
using Kadari.Application.Features.CareOps.ResolveOpsWorkItem;
using Kadari.Application.Features.CareOps.Services;
using Kadari.Application.Features.CareOps.SetCaseRoute;
using Kadari.Application.Features.CareOps.TakeOpsWorkItem;
using Kadari.Domain.Access;
using Kadari.WebApi.Access;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// Care Ops (CARE/11, CARE/14 C12): the staff queue of exceptions automation cannot safely finish — unknown routes,
/// safety reviews, failed registrations, rule conflicts, stalled cases — and their resolution; and the aggregate Care
/// metrics (C14). Staff only
/// (<c>CareOpsReview</c>), behind the <c>Care</c> launch switch. Enum values are names.
/// </summary>
public static class CareOpsEndpoints
{
    public static IEndpointRouteBuilder MapCareOpsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kadari/care-ops/work-items")
            .WithTags("Care Ops")
            .RequireFeature(features => features.Care)
            .RequirePermission(Permission.CareOpsReview);

        group.MapGet("/", async (bool? resolved, string? workType, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetOpsQueueQuery(resolved ?? false, workType), ct)))
            .Produces<IReadOnlyList<OpsWorkItemDto>>()
            .WithName("GetOpsQueue")
            .WithSummary("Open Care Ops work by severity then age (?resolved=true for done work, ?workType= to filter).");

        group.MapGet("/{workItemId:guid}", async (Guid workItemId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetOpsWorkItemQuery(workItemId), ct)))
            .Produces<OpsWorkItemDetailDto>()
            .WithName("GetOpsWorkItem")
            .WithSummary("One work item with only what the task needs: product facts, cover, the case's route and timeline.");

        group.MapPost("/{workItemId:guid}/take", async (Guid workItemId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new TakeOpsWorkItemCommand(workItemId), ct)))
            .Produces<OpsWorkItemDto>()
            .WithName("TakeOpsWorkItem")
            .WithSummary("Take a work item.");

        group.MapPost("/{workItemId:guid}/resolve", async (Guid workItemId, ResolveOpsRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ResolveOpsWorkItemCommand(workItemId, body.Resolution, body.Notes), ct)))
            .Produces<OpsWorkItemDto>()
            .WithName("ResolveOpsWorkItem")
            .WithSummary("Resolve a work item, marking what kind of knowledge it produced.");

        group.MapPost("/{workItemId:guid}/route", async (Guid workItemId, SetCaseRouteRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SetCaseRouteCommand(
                    workItemId, body.RouteType, body.TargetName, body.ChannelType, body.ChannelValue, body.Instructions,
                    body.Resolution, body.Notes), ct)))
            .Produces<OpsWorkItemDto>()
            .WithName("SetCaseRoute")
            .WithSummary("Give an unknown-route case the route an operator found; the owner confirms it before sending.");

        app.MapGet("/api/v1/kadari/care-ops/metrics", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCareMetricsQuery(), ct)))
            .WithTags("Care Ops")
            .RequireFeature(features => features.Care)
            .RequirePermission(Permission.CareMetricsView)
            .Produces<CareMetricsDto>()
            .WithName("GetCareMetrics")
            .WithSummary("How KADARI Care is performing: coverage found, registrations, cases, outcomes and manual interventions — aggregate counts only.");

        return group;
    }

    /// <summary>Resolution by name: ReusableKnowledge, CaseSpecific, PolicyReview, ProductBug.</summary>
    public sealed record ResolveOpsRequest(string Resolution, string Notes);

    public sealed record SetCaseRouteRequest(
        string RouteType, string TargetName, string ChannelType, string? ChannelValue, string Instructions, string Resolution, string Notes);
}
