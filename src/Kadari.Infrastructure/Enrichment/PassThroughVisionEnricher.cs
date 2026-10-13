using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// The default enricher: hands the on-device detections straight back, unenriched (ported from PoziSnap).
/// Keeping the pass-through as the default means the capture flow is exercisable end to end without a
/// Gemini key — a checked-out repo runs the camera. The Gemini implementation (2.3b) replaces this
/// registration when a key is configured.
/// </summary>
public sealed class PassThroughVisionEnricher : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Every item is one the device found, so each carries its own index back.
        var items = request.Detections
            .Select((detection, index) => IdentifiedItem.FromDetection(detection, index))
            .ToArray();

        return Task.FromResult(new VisionEnrichmentResult(Enriched: false, items));
    }
}
