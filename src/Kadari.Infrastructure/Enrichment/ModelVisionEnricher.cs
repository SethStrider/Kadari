using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// Identifies and values the household assets in a snap through the configured <see cref="IModelGateway"/> (Gemini or
/// an OpenAI-compatible endpoint). This class owns only the capture prompt and the reply mapping; the transport,
/// timeout and concurrency gate are the gateway's. The golden rule holds: a failure never loses the snap — the raw
/// detections come back as "awaiting enrichment". Whatever the call used is reported back in
/// <see cref="VisionEnrichmentResult.Usage"/> so capture records it like every other model call.
/// </summary>
public sealed class ModelVisionEnricher(
    IModelGateway gateway,
    IOptions<VisionEnricherOptions> options,
    ILogger<ModelVisionEnricher> logger) : IVisionEnricher
{
    public const string Feature = "capture";

    private readonly VisionEnricherOptions _options = options.Value;

    public bool IsCrossBorder => gateway.IsCrossBorder;

    /// <summary>Every hosted model call is billed, so it counts against the daily quota (AS-09).</summary>
    public bool IsMetered => true;

    public async Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unenriched = request.Detections
            .Select((detection, index) => IdentifiedItem.FromDetection(detection, index))
            .ToArray();

        byte[] image;
        await using (var stream = request.Image.OpenRead())
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            image = buffer.ToArray();
        }

        var reply = await gateway.CompleteAsync(
            new ModelCall(Feature, AssetEnrichmentPrompt.System, AssetEnrichmentPrompt.ForRequest(request, _options.MaxItems),
                new ModelAttachment(request.Image.ContentType, image)),
            cancellationToken).ConfigureAwait(false);

        if (!reply.Answered)
        {
            logger.LogWarning("Enrichment fell back to the raw detections: {Reason}", reply.FailureReason);
            return new VisionEnrichmentResult(Enriched: false, unenriched) { Usage = reply.Usage };
        }

        var items = AssetEnrichmentReplyMapper.Map(
            reply.Text, request.Detections, _options.MaxItems, _options.MaxReplacementValueMajor);

        if (items is null)
        {
            logger.LogWarning("Enrichment returned nothing usable; returning raw detections");
            return new VisionEnrichmentResult(Enriched: false, unenriched) { Usage = reply.Usage };
        }

        return new VisionEnrichmentResult(Enriched: true, items) { Usage = reply.Usage };
    }
}
