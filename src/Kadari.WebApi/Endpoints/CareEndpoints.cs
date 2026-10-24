using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.Assets.GetAssetHistory;
using Kadari.Application.Features.Assets.ReadProductLabel;
using Kadari.Application.Features.Assets.ReadReceipt;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Assets.RecordPurchaseDetails;
using Kadari.Application.Features.Care.AuthoriseRegistration;
using Kadari.Application.Features.Care.CancelRegistration;
using Kadari.Application.Features.Care.CompleteCareReminder;
using Kadari.Application.Features.Care.CompleteRegistration;
using Kadari.Application.Features.Care.DismissCareReminder;
using Kadari.Application.Features.Care.GetCareReminders;
using Kadari.Application.Features.Care.SnoozeCareReminder;
using Kadari.Application.Features.Care.DismissOpportunity;
using Kadari.Application.Features.Care.EnableCare;
using Kadari.Application.Features.Care.GetRegistration;
using Kadari.Application.Features.Care.ReportRegistrationProblem;
using Kadari.Application.Features.Care.Services;
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

        group.MapGet("/care/registration", async (Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetRegistrationQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.CareView)
            .Produces<RegistrationOverviewDto>()
            .WithName("GetRegistration")
            .WithSummary("Extra cover for the asset: open offers (benefit, deadline, who receives what data, the wording to agree to) and every registration.");

        group.MapPost("/care/registration-opportunities/{opportunityId:guid}/authorise", async (
                Guid customerId, Guid assetId, Guid opportunityId, AuthoriseRegistrationRequest body,
                [FromHeader(Name = IdempotencyKeyFilter.HeaderName)] string idempotencyKey, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new AuthoriseRegistrationCommand(
                    customerId, assetId, opportunityId, body.AcceptedTermsVersion, body.ExplicitConsent, idempotencyKey), ct)))
            .RequirePermission(Permission.CareManage)
            .AddEndpointFilter<IdempotencyKeyFilter>()
            .Produces<RegistrationDto>()
            .WithName("AuthoriseRegistration")
            .WithSummary("Authorise KADARI to register the product for extra cover. Needs explicit consent to the current wording and an Idempotency-Key; a replay returns the same registration.");

        group.MapPost("/care/registrations/{registrationId:guid}/complete", async (
                Guid customerId, Guid assetId, Guid registrationId, CompleteRegistrationRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new CompleteRegistrationCommand(
                    customerId, assetId, registrationId, body.ExternalReference, body.RegisteredOn), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CompleteRegistrationResponse>()
            .WithName("CompleteRegistration")
            .WithSummary("Record the provider's confirmation reference. Without it a registration is never complete; with it the cover is worked out again.");

        group.MapPost("/care/registrations/{registrationId:guid}/problem", async (
                Guid customerId, Guid assetId, Guid registrationId, RegistrationProblemRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ReportRegistrationProblemCommand(
                    customerId, assetId, registrationId, body.Reason, body.ProviderUnavailable), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<RegistrationStatusResponse>()
            .WithName("ReportRegistrationProblem")
            .WithSummary("The registration did not work. Provider unavailable keeps it open to retry; anything else goes to a person.");

        group.MapPost("/care/registrations/{registrationId:guid}/cancel", async (
                Guid customerId, Guid assetId, Guid registrationId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new CancelRegistrationCommand(customerId, assetId, registrationId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<RegistrationStatusResponse>()
            .WithName("CancelRegistration")
            .WithSummary("Cancel a registration before it is confirmed; its authority is revoked.");

        group.MapPost("/care/registration-opportunities/{opportunityId:guid}/dismiss", async (
                Guid customerId, Guid assetId, Guid opportunityId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new DismissOpportunityCommand(customerId, assetId, opportunityId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<DismissOpportunityResponse>()
            .WithName("DismissRegistrationOpportunity")
            .WithSummary("Not now: the offer is not shown again for this asset.");

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

        // Reminders span the customer's ledger (the "needs attention" list and notification deep links).
        var reminders = app.MapGroup("/api/v1/kadari/customers/{customerId:guid}/care/reminders")
            .WithTags("Care")
            .RequireFeature(features => features.Care);

        reminders.MapGet("/", async (Guid customerId, Guid? assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCareRemindersQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.CareView)
            .Produces<IReadOnlyList<CareReminderDto>>()
            .WithName("GetCareReminders")
            .WithSummary("Care reminders that need attention now, across the ledger or for one asset (?assetId=).");

        reminders.MapPost("/{reminderId:guid}/snooze", async (
                Guid customerId, Guid reminderId, SnoozeReminderRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SnoozeCareReminderCommand(customerId, reminderId, body.Days), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CareReminderStatusResponse>()
            .WithName("SnoozeCareReminder")
            .WithSummary("Remind me later: the reminder comes back after the given number of days (1–90).");

        reminders.MapPost("/{reminderId:guid}/dismiss", async (Guid customerId, Guid reminderId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new DismissCareReminderCommand(customerId, reminderId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CareReminderStatusResponse>()
            .WithName("DismissCareReminder")
            .WithSummary("Dismiss a reminder.");

        reminders.MapPost("/{reminderId:guid}/complete", async (Guid customerId, Guid reminderId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new CompleteCareReminderCommand(customerId, reminderId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CareReminderStatusResponse>()
            .WithName("CompleteCareReminder")
            .WithSummary("Mark a reminder done.");

        return group;
    }

    /// <summary>Body of the snooze endpoint.</summary>
    public sealed record SnoozeReminderRequest(int Days);

    /// <summary>Body of the enable-Care endpoint — the Care category by name (e.g. "MajorAppliance").</summary>
    public sealed record EnableCareRequest(string Category);

    /// <summary>Body of the authorise-registration endpoint: the wording version the owner read, and their consent.</summary>
    public sealed record AuthoriseRegistrationRequest(string AcceptedTermsVersion, bool ExplicitConsent);

    /// <summary>Body of the complete-registration endpoint: the provider's reference and the registration date.</summary>
    public sealed record CompleteRegistrationRequest(string ExternalReference, DateOnly RegisteredOn);

    /// <summary>Body of the registration-problem endpoint.</summary>
    public sealed record RegistrationProblemRequest(string Reason, bool ProviderUnavailable);

    /// <summary>Body of the purchase-details endpoint. Every field optional; at least one required.</summary>
    public sealed record PurchaseDetailsRequest(string? Brand, string? Model, string? Retailer, DateOnly? PurchasedOn);
}
