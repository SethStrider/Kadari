using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.Assets.GetAssetHistory;
using Kadari.Application.Features.Assets.ReadProductLabel;
using Kadari.Application.Features.Assets.ReadReceipt;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Assets.RecordPurchaseDetails;
using Kadari.Application.Features.Care.EnableCare;
using Kadari.Application.Features.Care.EvaluateCare;
using Kadari.Application.Features.Care.GetCareSummary;
using Kadari.Application.Features.Care.GetCoverage;
using Kadari.Domain.Access;
using Kadari.WebApi.Access;
using Kadari.WebApi.Http;
using Kadari.WebApi.RateLimiting;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// KADARI Care on an existing asset (CARE/13, additive to the asset routes): the Care card, enabling Care, coverage
/// and its re-evaluation, the purchase details Care reads, and the asset's permanent history. Customer self-service —
/// ownership is enforced by the pipeline (PAS-002 §2); the whole group sits behind the <c>Care</c> launch switch.
/// </summary>
public static class CareEndpoints
{
    public static IEndpointRouteBuilder MapCareEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kadari/customers/{customerId:guid}/assets/{assetId:guid}")
            .WithTags("Care")
            .RequireFeature(features => features.Care);

        group.MapGet("/care", async (Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCareSummaryQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.CareView)
            .Produces<CareSummaryDto>()
            .WithName("GetCareSummary")
            .WithSummary("The Care card for an asset: coverage headline, next important date, open cases, missing evidence and one next action.");

        group.MapPost("/care", async (
                Guid customerId, Guid assetId, EnableCareRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new EnableCareCommand(customerId, assetId, body.Category), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<EnableCareResponse>()
            .WithName("EnableCare")
            .WithSummary("Switch KADARI Care on for an asset (idempotent) and work out its cover from the registry.");

        group.MapPost("/care/evaluate", async (Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new EvaluateCareCommand(customerId, assetId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<EvaluateCareResponse>()
            .WithName("EvaluateCare")
            .WithSummary("Re-run the coverage engine against the asset's latest evidence and the current published rules. Appends a decision; never rewrites one.");

        group.MapGet("/care/coverage", async (Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCoverageQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.CareView)
            .Produces<CoverageDetailDto>()
            .WithName("GetCoverage")
            .WithSummary("Each kind of cover KADARI found, with its dates, summary and how KADARI knows it (source, verification, rule version).");

        group.MapPost("/purchase", async (
                Guid customerId, Guid assetId, PurchaseDetailsRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new RecordPurchaseDetailsCommand(
                    customerId, assetId, body.Brand, body.Model, body.Retailer, body.PurchasedOn), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<RecordPurchaseDetailsResponse>()
            .WithName("RecordPurchaseDetails")
            .WithSummary("Record what was bought, where and when. Appended as evidence (a correction never overwrites); Care re-evaluates.");

        group.MapPost("/purchase/read-receipt", async (Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ReadReceiptCommand(customerId, assetId), ct)))
            .RequirePermission(Permission.CareManage)
            .RequireRateLimiting(RateLimitingExtensions.SnapCapturePolicy)   // each read is a billed model call
            .Produces<PurchaseProposalDto>()
            .WithName("ReadReceipt")
            .WithSummary("Read the asset's stored receipt with AI and propose the purchase details for the owner to confirm. Saves nothing but the model usage.");

        group.MapPost("/purchase/read-label", async (
                Guid customerId, Guid assetId, IFormFile photo, ISender sender, CancellationToken ct) =>
            {
                // Spill the upload to a temp file for the disk-backed SnapImage; dispose deletes it.
                var tempPath = Path.GetTempFileName();
                await using (var file = File.Create(tempPath))
                {
                    await photo.CopyToAsync(file, ct);
                }

                var image = new SnapImage(tempPath, photo.ContentType, photo.Length);
                try
                {
                    return Results.Ok(await sender.Send(new ReadProductLabelCommand(customerId, assetId, image), ct));
                }
                finally
                {
                    await image.DisposeAsync();
                }
            })
            .RequirePermission(Permission.CareManage)
            .RequireRateLimiting(RateLimitingExtensions.SnapCapturePolicy)   // each read is a billed model call
            .DisableAntiforgery()   // minimal-API multipart upload
            .AllowUploads()
            .Produces<PurchaseProposalDto>()
            .WithName("ReadProductLabel")
            .WithSummary("Read a photo of the rating plate or serial label with AI and propose brand, model and serial. The photo is not stored.");

        group.MapGet("/history", async (Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetAssetHistoryQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.CareView)
            .Produces<AssetHistoryDto>()
            .WithName("GetAssetHistory")
            .WithSummary("The asset's permanent history, newest first: registration, transfers, theft and recovery, and material Care outcomes.");

        return group;
    }

    /// <summary>Body of the enable-Care endpoint — the Care category by name (e.g. "MajorAppliance").</summary>
    public sealed record EnableCareRequest(string Category);

    /// <summary>Body of the purchase-details endpoint. Every field optional; at least one required.</summary>
    public sealed record PurchaseDetailsRequest(string? Brand, string? Model, string? Retailer, DateOnly? PurchasedOn);
}
