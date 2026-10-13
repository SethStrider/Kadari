using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Tenancy;
using Kadari.Application.Features.Valuation;
using Kadari.Application.Features.Subjects.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>
/// Analyse one captured still for a customer: store the evidence image, enrich the on-device detections
/// into identified assets, and register each into the customer's ledger (deck §13–14). Customer-scoped —
/// the caller may only capture into their own ledger.
/// </summary>
/// <remarks>The endpoint owns the <see cref="SnapImage"/> lifetime and disposes it after this returns.</remarks>
public sealed record AnalyzeSnapCommand(
    Guid CustomerId,
    SnapImage Image,
    IReadOnlyList<DetectedObject> Detections,
    string? RoomLabel) : ICommand<AnalyzeSnapResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record AnalyzeSnapResponse(Guid SnapId, bool Enriched, IReadOnlyList<Guid> AssetIds);

internal sealed class AnalyzeSnapHandler(
    IKadariDbContext db,
    IBlobStorage blobs,
    IVisionEnricher enricher,
    ValuationRailRouter valuation,
    TenantMembershipResolver tenants,
    SubjectRegistrar subjects,
    SnapCaptureWork captureWork,
    VisionQuota quota,
    TimeProvider clock) : ICommandHandler<AnalyzeSnapCommand, AnalyzeSnapResponse>
{
    private const string EnricherSource = "gemini";

    public async Task<AnalyzeSnapResponse> Handle(AnalyzeSnapCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var customerId = new CustomerId(cmd.CustomerId);

        // The base lawful basis (spec §6, POPIA): no image of the customer's home is stored or analysed until
        // they have agreed to it. Checked before the upload, so a refused capture leaves nothing behind.
        if (!await HasConsentAsync(customerId, KadariConsentScope.AssetImageProcessing, ct))
        {
            throw new ConsentRequiredException(nameof(KadariConsentScope.AssetImageProcessing));
        }

        // External work — the stored still and the vision call — happens once per capture, even when the
        // transaction around this handler is retried (GL-35). The database writes below are redone on a retry.
        var captured = await captureWork.OnceAsync(cmd, () => CaptureAsync(cmd, customerId, ct));

        // Counted here, not inside the once-only capture work, so a retried transaction counts it again with
        // the rest of its writes rather than losing it.
        if (captured.MeteredCall)
        {
            await quota.RecordCallAsync(customerId, ct);
        }

        var ownerId = await subjects.GetOrCreateForCustomerAsync(customerId, ct);
        var tenantId = await tenants.ResolveForCustomerAsync(customerId, ct);

        var assetIds = new List<Guid>(captured.Enrichment.Items.Count);
        foreach (var item in captured.Enrichment.Items)
        {
            var asset = CapturedAssetFactory.Create(
                tenantId, ownerId, captured.ImageKey, EnricherSource, item, valuation.Value(item), now, cmd.RoomLabel);
            db.Assets.Add(asset);
            assetIds.Add(asset.Id.Value);
        }

        await db.SaveChangesAsync(ct);

        return new AnalyzeSnapResponse(captured.SnapId, captured.Enrichment.Enriched, assetIds);
    }

    private async Task<CapturedSnap> CaptureAsync(AnalyzeSnapCommand cmd, CustomerId customerId, CancellationToken ct)
    {
        var snapId = Guid.CreateVersion7();

        // Strip GPS/camera metadata first (AS-06): neither storage nor the vision model may see where the photo
        // was taken.
        await cmd.Image.StripMetadataAsync(ct);

        // Cross-border disclosure gate (D8/POPIA): a still may only be sent to an off-shore AI when the
        // customer has granted the distinct cross-border-AI consent. Without it, keep the image on-shore and
        // fall back to the on-device detections — capture still succeeds, nothing leaves the country.
        // The daily vision quota (AS-09) falls back the same way: past it, nothing is billed.
        var crossBorderAllowed = !enricher.IsCrossBorder
            || await HasConsentAsync(customerId, KadariConsentScope.CrossBorderAiEnrichment, ct);
        var withinQuota = crossBorderAllowed && (!enricher.IsMetered || await quota.HasRoomAsync(customerId, ct));
        var mayEnrich = crossBorderAllowed && withinQuota;

        var enrichment = mayEnrich
            ? await enricher.EnrichAsync(new VisionEnrichmentRequest(cmd.Image, cmd.Detections, cmd.RoomLabel), ct)
            : new VisionEnrichmentResult(
                Enriched: false,
                cmd.Detections.Select((d, i) => IdentifiedItem.FromDetection(d, i)).ToList());

        // Store the canonical still (D8 — Kadari keeps pre-loss evidence, unlike PoziSnap which stores
        // nothing at analyze time) — but only when an asset will point at it (AS-16). A snap that found
        // nothing would otherwise leave a photo of someone's home that no record refers to.
        var imageKey = $"kadari/{cmd.CustomerId:N}/{snapId:N}.img";
        if (enrichment.Items.Count > 0)
        {
            await using var stream = cmd.Image.OpenRead();
            await blobs.UploadAsync(imageKey, stream, cmd.Image.ContentType, ct);
        }

        SnapMetrics.Record(
            !crossBorderAllowed ? "no_cross_border_consent"
                : !withinQuota ? "quota_reached"
                : enrichment.Enriched ? "enriched"
                : "enricher_fallback",
            foundItems: enrichment.Items.Count > 0);

        return new CapturedSnap(snapId, imageKey, enrichment, MeteredCall: mayEnrich && enricher.IsMetered);
    }

    private Task<bool> HasConsentAsync(CustomerId customerId, KadariConsentScope scope, CancellationToken ct) =>
        db.KadariConsents.AsNoTracking().AnyAsync(
            c => c.CustomerId == customerId && c.Scope == scope && c.Status == KadariConsentStatus.Granted, ct);
}
