using Microsoft.Extensions.Options;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>Holds the process-wide model-call semaphore, shared by every model gateway across requests.</summary>
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
