namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>What one capture did outside the database: where its still is stored, and what the enricher said.</summary>
internal sealed record CapturedSnap(Guid SnapId, string ImageKey, VisionEnrichmentResult Enrichment, bool MeteredCall);

/// <summary>
/// Remembers a capture's external work for the life of the request (GL-35). Every command runs inside a
/// transaction that the execution strategy re-runs on a transient database failure; without this, each re-run
/// uploaded the still again under a new key (an orphaned copy of someone's home) and called the vision model again
/// (billed again). The database writes are redone on a retry, as they must be; the external work is not.
/// </summary>
/// <remarks>Scoped: one request scope, one capture. Keyed by the command instance a retry passes through again.</remarks>
internal sealed class SnapCaptureWork
{
    private readonly Dictionary<object, CapturedSnap> _done = new(ReferenceEqualityComparer.Instance);

    public async Task<CapturedSnap> OnceAsync(AnalyzeSnapCommand command, Func<Task<CapturedSnap>> work)
    {
        if (_done.TryGetValue(command, out var captured))
        {
            return captured;
        }

        // Only a completed result is remembered: if the work itself failed, a retry does it again.
        captured = await work();
        _done[command] = captured;
        return captured;
    }
}
