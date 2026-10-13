using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;

namespace Kadari.Application.Features.PurgeEphemeralFrames;

/// <summary>
/// POPIA data minimisation for Kadari (D8): purge every ephemeral scan frame past its retention window,
/// deleting its blob and clearing its reference, while leaving canonical evidence images (no retention
/// window) untouched — those are kept long-term on the insurance lawful basis. System-initiated by the
/// worker's periodic sweep, so <see cref="IUnscopedRequest"/>. Idempotent — an already-purged frame is skipped.
/// </summary>
public sealed record PurgeEphemeralFramesCommand : ICommand<PurgeEphemeralFramesResponse>, IUnscopedRequest;

/// <param name="Assets">How many assets had ephemeral frames purged.</param>
/// <param name="Frames">How many ephemeral frames were purged in total.</param>
public sealed record PurgeEphemeralFramesResponse(int Assets, int Frames);

internal sealed class PurgeEphemeralFramesHandler(
    IKadariDbContext db, IBlobStorage blobs, TimeProvider clock, ILogger<PurgeEphemeralFramesHandler> logger)
    : ICommandHandler<PurgeEphemeralFramesCommand, PurgeEphemeralFramesResponse>
{
    public async Task<PurgeEphemeralFramesResponse> Handle(PurgeEphemeralFramesCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var assets = await db.Assets
            .Include(a => a.Evidence)
            .Where(a => a.Evidence.Any(e => e.RetentionUntil != null && e.RetentionUntil < now && e.PurgedAt == null))
            .ToListAsync(ct);

        var frames = 0;
        foreach (var asset in assets)
        {
            foreach (var key in asset.PurgeExpiredEphemeralEvidence(now))
            {
                frames++;
                try
                {
                    await blobs.DeleteAsync(key, ct);
                }
                catch (Exception ex)
                {
                    // Best-effort — the frame is still marked purged; an orphaned blob can be swept later.
                    logger.LogWarning(ex, "Ephemeral-frame purge: blob delete failed for {Key}.", key);
                }
            }
        }

        if (assets.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new PurgeEphemeralFramesResponse(assets.Count, frames);
    }
}
