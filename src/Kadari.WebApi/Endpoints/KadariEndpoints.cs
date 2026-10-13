using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Kadari.Application.Features.AddHouseholdMember;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.AssignTenant;
using Kadari.Application.Features.CreateInsurerTenant;
using Kadari.Application.Features.GetInsurerContext;
using Kadari.Application.Features.SetTenantBranding;
using Kadari.Application.Common.Pagination;
using Kadari.Application.Features.Exit;
using Kadari.Application.Features.RemoveHouseholdMember;
using Kadari.Application.Features.GetAsset;
using Kadari.Application.Features.GetAssetPassport;
using Kadari.Application.Features.GetCustomerBranding;
using Kadari.Application.Features.GetLedger;
using Kadari.Application.Features.GetTenantPortfolio;
using Kadari.Application.Features.GetConsents;
using Kadari.Application.Features.GrantConsent;
using Kadari.Application.Features.GrantInsurerAccess;
using Kadari.Application.Features.ListTenantAssets;
using Kadari.Application.Features.MintAssetPassport;
using Kadari.Application.Features.RecordOwnershipProof;
using Kadari.Application.Features.SettleEstate;
using Kadari.Application.Features.SubmitValuation;
using Kadari.Application.Features.TransferAsset;
using Kadari.Application.Features.VerifyAssetPassport;
using Kadari.Application.Features.WithdrawConsent;
using Kadari.Domain.Assets;
using Kadari.Domain.Access;
using Kadari.WebApi.Access;
using Kadari.WebApi.Http;
using Kadari.WebApi.RateLimiting;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// Kadari endpoints (deck §13–14): capture a still into the customer's ledger, and read the Live Asset
/// Ledger back. Customer self-service — ownership is enforced by the pipeline (PAS-002 §2).
/// </summary>
public static class KadariEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapKadariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kadari/customers/{customerId:guid}").WithTags("Kadari");

        group.MapPost("/snaps", async (
                Guid customerId,
                IFormFile image,
                [FromForm] string? detections,
                [FromForm] string? roomLabel,
                ISender sender,
                CancellationToken ct) =>
            {
                // Spill the upload to a temp file for the disk-backed SnapImage; dispose deletes it.
                var tempPath = Path.GetTempFileName();
                await using (var file = File.Create(tempPath))
                {
                    await image.CopyToAsync(file, ct);
                }

                var snap = new SnapImage(tempPath, image.ContentType, image.Length);
                try
                {
                    var result = await sender.Send(
                        new AnalyzeSnapCommand(customerId, snap, ParseDetections(detections), roomLabel), ct);
                    return Results.Ok(result);
                }
                finally
                {
                    await snap.DisposeAsync();
                }
            })
            .RequirePermission(Permission.AssetCapture)
            .RequireRateLimiting(RateLimitingExtensions.SnapCapturePolicy)
            .DisableAntiforgery()   // minimal-API multipart upload
            .AllowUploads()
            .WithName("AnalyzeSnap")
            .WithSummary("Analyse a captured still: store the evidence image, enrich the on-device detections, and register identified assets.");

        group.MapGet("/ledger", async (Guid customerId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetLedgerQuery(customerId), ct)))
            .RequirePermission(Permission.AssetView)
            .WithName("GetLedger")
            .WithSummary("Read the customer's Live Asset Ledger (rooms, replacement exposure, completeness).");

        group.MapGet("/branding", async (Guid customerId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCustomerBrandingQuery(customerId), ct)))
            .RequirePermission(Permission.AssetView)
            .Produces<CustomerBrandingDto>()
            .WithName("GetCustomerBranding")
            .WithSummary("Read the insurer white-label branding for the customer's KADARI app (nulls when consumer-direct).");

        group.MapGet("/assets/{assetId:guid}", async (
                Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetAssetQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.AssetView)
            .Produces<AssetDetailDto>()
            .WithName("GetAsset")
            .WithSummary("Read one asset in the customer's ledger, with its full evidence set.");

        group.MapPost("/assets/{assetId:guid}/passport", async (
                Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new MintAssetPassportCommand(customerId, assetId), ct)))
            .RequirePermission(Permission.AssetCapture)
            .Produces<AssetPassportDto>()
            .WithName("MintAssetPassport")
            .WithSummary("Mint (or refresh) the asset's externally-verifiable Asset Passport from its evidence.");

        group.MapGet("/assets/{assetId:guid}/passport", async (
                Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetAssetPassportQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.AssetView)
            .Produces<AssetPassportDto>()
            .WithName("GetAssetPassport")
            .WithSummary("Read the asset's Asset Passport credential.");

        group.MapGet("/assets/{assetId:guid}/export", async (
                Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ExportAssetForSaleQuery(customerId, assetId), ct)))
            .RequireFeature(features => features.AssetOwnershipChanges)
            .RequirePermission(Permission.AssetExport)
            .WithName("ExportAssetForSale")
            .WithSummary("Produce the signed, verifiable asset-for-sale export (exit-to-PoziSnap). Requires a minted passport.");

        group.MapPost("/assets/{assetId:guid}/members", async (
                Guid customerId, Guid assetId, HouseholdMemberRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new AddHouseholdMemberCommand(customerId, assetId, body.MemberCustomerId), ct)))
            .RequireFeature(features => features.AssetOwnershipChanges)
            .RequirePermission(Permission.AssetCapture)
            .WithName("AddHouseholdMember")
            .WithSummary("Share an asset with a household member — they co-own it and it appears in their ledger.");

        group.MapDelete("/assets/{assetId:guid}/members/{memberCustomerId:guid}", async (
                Guid customerId, Guid assetId, Guid memberCustomerId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new RemoveHouseholdMemberCommand(customerId, assetId, memberCustomerId), ct)))
            .RequireFeature(features => features.AssetOwnershipChanges)
            .RequirePermission(Permission.AssetCapture)
            .WithName("RemoveHouseholdMember")
            .WithSummary("Remove a household member from an asset — they stop co-owning it.");

        group.MapPost("/assets/{assetId:guid}/transfer", async (
                Guid customerId, Guid assetId, TransferAssetRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new TransferAssetCommand(customerId, assetId, body.RecipientCustomerId), ct)))
            .RequireFeature(features => features.AssetOwnershipChanges)
            .RequirePermission(Permission.AssetTransfer)
            .WithName("TransferAsset")
            .WithSummary("Transfer one of the customer's assets to another customer (a gift or private sale).");

        group.MapPost("/assets/{assetId:guid}/proof", async (
                Guid customerId,
                Guid assetId,
                IFormFile? receipt,
                [FromForm] string? serialNumber,
                [FromForm] long? amountMinor,
                [FromForm] string? currency,
                ISender sender,
                CancellationToken ct) =>
            {
                ReceiptDocument? document = null;
                if (receipt is not null)
                {
                    var tempPath = Path.GetTempFileName();
                    await using (var file = File.Create(tempPath))
                    {
                        await receipt.CopyToAsync(file, ct);
                    }

                    document = new ReceiptDocument(tempPath, receipt.ContentType, receipt.Length);
                }

                try
                {
                    var result = await sender.Send(new RecordOwnershipProofCommand(
                        customerId, assetId, document, serialNumber, amountMinor, currency), ct);
                    return Results.Ok(result);
                }
                finally
                {
                    if (document is not null)
                    {
                        await document.DisposeAsync();
                    }
                }
            })
            .RequireRateLimiting(RateLimitingExtensions.SnapCapturePolicy)   // AS-13: each proof is a stored file
            .RequirePermission(Permission.AssetCapture)
            .DisableAntiforgery()   // minimal-API multipart upload
            .AllowUploads()
            .Produces<RecordOwnershipProofResponse>()
            .WithName("RecordOwnershipProof")
            .WithSummary("Attach independent proof of ownership (receipt and/or serial, optional receipt price) to lift the asset's assurance.");

        group.MapPost("/consents", async (
                Guid customerId, GrantConsentRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GrantKadariConsentCommand(customerId, body.Scope), ct)))
            .RequirePermission(Permission.AssetCapture)
            .Produces<GrantKadariConsentResponse>()
            .WithName("GrantKadariConsent")
            .WithSummary("Record the customer's Kadari consent for a scope (incl. the cross-border-AI disclosure).");

        group.MapGet("/consents", async (Guid customerId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetKadariConsentsQuery(customerId), ct)))
            .RequirePermission(Permission.AssetView)
            .Produces<IReadOnlyList<KadariConsentDto>>()
            .WithName("GetKadariConsents")
            .WithSummary("Every Kadari consent scope and whether the customer currently grants it.");

        group.MapPost("/consents/withdraw", async (
                Guid customerId, GrantConsentRequest body, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new WithdrawKadariConsentCommand(customerId, body.Scope), ct);
                return Results.NoContent();
            })
            .RequirePermission(Permission.AssetCapture)
            .WithName("WithdrawKadariConsent")
            .WithSummary("Withdraw one Kadari consent scope. Stops new processing; does not erase what is held.");

        // Valuer-facing (staff), not under a customer path — a professional valuer appraises across customers.
        var valuations = app.MapGroup("/api/v1/kadari/assets/{assetId:guid}").WithTags("Kadari");

        valuations.MapPost("/valuations", async (
                Guid assetId, SubmitValuationRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SubmitAssetValuationCommand(
                    assetId, body.AmountMinor, body.Currency, body.Valuer, body.CertificateReference), ct)))
            .RequirePermission(Permission.AssetValuate)
            .WithName("SubmitAssetValuation")
            .WithSummary("Submit a professional (independent) valuation for an asset, upgrading its assurance.");

        // Insurer console — tenant-level actions not keyed by a path id: the signed-in insurer's own context
        // (what the console themes itself with) and onboarding a new insurer tenant.
        var insurerRoot = app.MapGroup("/api/v1/kadari/insurer").WithTags("Kadari");

        insurerRoot.MapGet("/context", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetInsurerContextQuery(), ct)))
            .RequireFeature(features => features.InsurerConsole)
            .RequirePermission(Permission.InsurerPortfolioView)
            .Produces<InsurerContextDto>()
            .WithName("GetInsurerContext")
            .WithSummary("The signed-in insurer's own tenant + white-label branding (what the console themes itself with).");

        insurerRoot.MapPost("/tenants", async (
                CreateInsurerTenantCommand cmd, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(cmd, ct);
                return Results.Created($"/api/v1/kadari/insurer/tenants/{result.TenantId}", result);
            })
            .RequireFeature(features => features.InsurerConsole)
            .RequirePermission(Permission.AssetTenantAssign)
            .WithName("CreateInsurerTenant")
            .WithSummary("Onboard an insurer as a white-label tenant (optionally with branding).");

        // Insurer console (staff oversight): cross-customer portfolio reads over a tenant's cohort.
        var insurer = app.MapGroup("/api/v1/kadari/insurer/tenants/{tenantId:guid}").WithTags("Kadari");

        insurer.MapGet("/portfolio", async (Guid tenantId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetTenantPortfolioQuery(tenantId), ct)))
            .RequireFeature(features => features.InsurerConsole)
            .RequirePermission(Permission.InsurerPortfolioView)
            .Produces<TenantPortfolioDto>()
            .WithName("GetTenantPortfolio")
            .WithSummary("Read an insurer tenant's whole-cohort portfolio: exposure, assurance distribution, per-category.");

        insurer.MapGet("/assets", async (
                [AsParameters] ListTenantAssetsQuery query, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(query, ct)))
            .RequireFeature(features => features.InsurerConsole)
            .RequirePermission(Permission.InsurerPortfolioView)
            .Produces<PagedResult<TenantAssetRowDto>>()
            .WithName("ListTenantAssets")
            .WithSummary("List the assets in an insurer tenant's cohort (paged), with each asset's owner and derived headline.");

        insurer.MapPost("/members", async (
                Guid tenantId, AssignMemberRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new AssignCustomerToTenantCommand(body.CustomerId, tenantId), ct)))
            .RequireFeature(features => features.InsurerConsole)
            .RequirePermission(Permission.AssetTenantAssign)
            .WithName("AssignCustomerToTenant")
            .WithSummary("Assign a customer's cohort to this tenant: future captures inherit it and existing assets are re-stamped.");

        insurer.MapPost("/users", async (
                Guid tenantId, GrantInsurerAccessRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GrantInsurerAccessCommand(body.UserAccountId, tenantId), ct)))
            .RequireFeature(features => features.InsurerConsole)
            .RequirePermission(Permission.AssetTenantAssign)
            .WithName("GrantInsurerAccess")
            .WithSummary("Provision a user account as an insurer-console user for this tenant (Insurer role + tenant binding).");

        insurer.MapPut("/branding", async (
                Guid tenantId, SetBrandingRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new SetTenantBrandingCommand(tenantId, body.PrimaryColor, body.AccentColor, body.LogoReference), ct)))
            .RequireFeature(features => features.InsurerConsole)
            .RequirePermission(Permission.AssetTenantAssign)
            .WithName("SetTenantBranding")
            .WithSummary("Set (or replace) this tenant's white-label branding for the insurer console.");

        // Estate settlement (staff): pass a deceased customer's whole cohort to an heir. Not under a customer
        // path — it acts across two customers.
        app.MapPost("/api/v1/kadari/estate/settle", async (
                SettleEstateRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SettleEstateCommand(body.DeceasedCustomerId, body.HeirCustomerId), ct)))
            .RequireFeature(features => features.AssetOwnershipChanges)
            .RequirePermission(Permission.AssetEstateSettle)
            .WithTags("Kadari")
            .WithName("SettleEstate")
            .WithSummary("Settle a deceased customer's estate: pass all their assets to an heir as inheritance.");

        // Public, no login: verify an Asset Passport by its AP- id (the QR / secure-link target). No owner data.
        app.MapGet("/api/v1/verify/asset-passports/{verificationId}",
                async (string verificationId, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new VerifyAssetPassportQuery(verificationId), ct)))
            .Produces<AssetPassportVerificationDto>()
            .WithTags("Kadari")
            .WithName("VerifyAssetPassport")
            .WithSummary("Independently verify a KADARI Asset Passport by its AP- id (QR / secure link).");

        // Public, no-login verification of an asset-for-sale export token (mirrors verify-passport). POST so the
        // token — which carries asset attributes — stays out of the URL.
        app.MapPost("/api/v1/verify/asset-exports", async (
                VerifyExportRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new VerifyAssetExportQuery(body.Token), ct)))
            .RequireFeature(features => features.AssetOwnershipChanges)
            .WithTags("Kadari")
            .WithName("VerifyAssetExport")
            .WithSummary("Independently verify an asset-for-sale export token (signature + live passport status).");

        return group;
    }

    /// <summary>Body of the grant / withdraw consent endpoints — the Kadari scope concerned.</summary>
    public sealed record GrantConsentRequest(KadariConsentScope Scope);

    /// <summary>Body of the professional-valuation endpoint.</summary>
    public sealed record SubmitValuationRequest(long AmountMinor, string Currency, string Valuer, string CertificateReference);

    /// <summary>Body of the assign-member endpoint — the customer being assigned to the tenant.</summary>
    public sealed record AssignMemberRequest(Guid CustomerId);

    /// <summary>Body of the grant-insurer-access endpoint — the user account to provision for the tenant.</summary>
    public sealed record GrantInsurerAccessRequest(Guid UserAccountId);

    /// <summary>Body of the set-branding endpoint — the tenant's white-label colours and optional logo.</summary>
    public sealed record SetBrandingRequest(string PrimaryColor, string AccentColor, string? LogoReference);

    /// <summary>Body of the public verify-export endpoint — the signed asset-for-sale token.</summary>
    public sealed record VerifyExportRequest(string Token);

    /// <summary>Body of the transfer endpoint — the customer receiving the asset.</summary>
    public sealed record TransferAssetRequest(Guid RecipientCustomerId);

    /// <summary>Body of the add-household-member endpoint — the customer to share the asset with.</summary>
    public sealed record HouseholdMemberRequest(Guid MemberCustomerId);

    /// <summary>Body of the estate-settlement endpoint — the deceased and their heir.</summary>
    public sealed record SettleEstateRequest(Guid DeceasedCustomerId, Guid HeirCustomerId);

    private static IReadOnlyList<DetectedObject> ParseDetections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<DetectedObject>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
