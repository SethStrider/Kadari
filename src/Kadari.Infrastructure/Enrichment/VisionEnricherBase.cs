using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// The provider-agnostic skeleton every model-backed enricher shares (ported from PoziSnap's Gemini
/// enricher): the concurrency gate, the timeout, the reply mapping, and the golden rule that a failure never
/// loses the snap — the raw detections come back as "awaiting enrichment". A provider supplies only
/// <see cref="CallModelAsync"/> (its HTTP call, returning the model's reply text).
/// </summary>
public abstract class VisionEnricherBase(
    VisionConcurrencyGate gate,
    IOptions<VisionEnricherOptions> options,
    ILogger logger) : IVisionEnricher
{
    protected VisionEnricherOptions Options { get; } = options.Value;

    /// <summary>Model-backed providers call an external AI; a provider that leaves the country (Gemini,
    /// hosted OpenAI-compatible) overrides this to true so the cross-border consent gate applies.</summary>
    public virtual bool IsCrossBorder => false;

    /// <summary>Every hosted model call is billed, so it counts against the daily quota (AS-09).</summary>
    public bool IsMetered => true;

    public async Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unenriched = request.Detections
            .Select((detection, index) => IdentifiedItem.FromDetection(detection, index))
            .ToArray();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Options.TimeoutSeconds));

        try
        {
            await gate.Semaphore.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Snap enrichment timed out waiting for a slot; returning raw detections");
            return new VisionEnrichmentResult(Enriched: false, unenriched);
        }

        try
        {
            var reply = await CallModelAsync(request, timeout.Token).ConfigureAwait(false);
            var items = AssetEnrichmentReplyMapper.Map(
                reply, request.Detections, Options.MaxItems, Options.MaxReplacementValueMajor);

            if (items is null)
            {
                logger.LogWarning("Enrichment returned nothing usable; returning raw detections");
                return new VisionEnrichmentResult(Enriched: false, unenriched);
            }

            return new VisionEnrichmentResult(Enriched: true, items);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Enrichment failed; returning raw detections");
            return new VisionEnrichmentResult(Enriched: false, unenriched);
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    /// <summary>Make the provider's call and return the model's reply text (the JSON the mapper parses), or
    /// null if there was nothing usable.</summary>
    protected abstract Task<string?> CallModelAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken);

    /// <summary>Reads the whole still off disk as base64 — shared by the HTTP providers.</summary>
    protected static async Task<string> ReadImageBase64Async(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        await using var stream = request.Image.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}

/// <summary>Holds the process-wide enrichment semaphore so an enricher can share one across requests.</summary>
public sealed class VisionConcurrencyGate : IDisposable
{
    public VisionConcurrencyGate(IOptions<VisionEnricherOptions> options)
    {
        var limit = options.Value.MaxConcurrentCalls;
        Semaphore = new SemaphoreSlim(limit, limit);
    }

    public SemaphoreSlim Semaphore { get; }

    public void Dispose() => Semaphore.Dispose();
}
