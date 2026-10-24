using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.DataRights;

public sealed record KadariErasureResult(int AssetsErased, int AssetsHandedOver, int FilesDeleted);

/// <summary>
/// The Kadari part of a customer's POPIA erasure (AS-03), run inside <c>EraseCustomer</c>'s transaction:
/// <list type="bullet">
/// <item>every Kadari consent is withdrawn, and the customer's subject (which carries their name) is anonymised;</item>
/// <item>an asset they own alone is erased — its photo, receipt and certificate files deleted, its Care profile
/// soft-deleted, the record
/// soft-deleted and its Asset Passport revoked, so the public check stops vouching for it;</item>
/// <item>an asset they share with a household is handed to the longest-standing co-owner, who owns it equally
/// (files kept — they are that person's evidence too), and its passport follows;</item>
/// <item>where they are only a co-owner, they are removed from the household.</item>
/// </list>
/// File deletes are best-effort, like the document blobs: the reference is cleared either way, so an orphaned
/// file is unreachable and can be swept later.
/// </summary>
public sealed class KadariErasure(IKadariDbContext db, IBlobStorage blobs, ILogger<KadariErasure> logger)
{
    public async Task<KadariErasureResult> EraseAsync(CustomerId customerId, DateTimeOffset now, CancellationToken ct)
    {
        var consents = await db.KadariConsents
            .Where(c => c.CustomerId == customerId && c.Status == KadariConsentStatus.Granted)
            .ToListAsync(ct);
        foreach (var consent in consents)
        {
            consent.Withdraw(now);
        }

        var subject = await db.Owners.FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);
        if (subject is null)
        {
            return new KadariErasureResult(0, 0, 0);   // never captured anything
        }

        // The subject carries the customer's name; assets, evidence and passports hang off it.
        subject.Anonymize();

        var subjectId = subject.Id;
        var assets = await db.Assets
            .Include(a => a.Evidence)
            .Include(a => a.CoOwners)
            .Where(a => a.OwnerId == subjectId || a.CoOwners.Any(c => c.MemberId == subjectId))
            .ToListAsync(ct);

        var assetIds = assets.Select(a => a.Id).ToList();
        var passports = await db.AssetPassports.Where(p => assetIds.Contains(p.AssetId)).ToListAsync(ct);
        var careProfiles = await db.CareProfiles.Where(p => assetIds.Contains(p.AssetId)).ToListAsync(ct);

        int erased = 0, handedOver = 0, files = 0;
        foreach (var asset in assets)
        {
            var passport = passports.FirstOrDefault(p => p.AssetId == asset.Id);

            if (asset.OwnerId != subjectId)
            {
                asset.RemoveCoOwner(subjectId, now);
                continue;
            }

            if (asset.HandOverToCoOwner(now) is { } successor)
            {
                passport?.ReassignOwner(successor, now);
                handedOver++;
                continue;
            }

            foreach (var key in asset.EraseEvidenceFiles(now))
            {
                files += await TryDeleteAsync(key, ct) ? 1 : 0;
            }

            passport?.Revoke(now);

            // Care goes with the asset. Its decisions and the asset's history are append-only and hold no personal
            // data (product facts and dates, like the evidence facts that stay), so they remain for the audit trail.
            if (careProfiles.FirstOrDefault(p => p.AssetId == asset.Id) is { } care)
            {
                db.CareProfiles.Remove(care);   // soft-delete
            }

            db.Assets.Remove(asset);   // soft-delete (with its evidence and household rows)
            erased++;
        }

        return new KadariErasureResult(erased, handedOver, files);
    }

    private async Task<bool> TryDeleteAsync(string reference, CancellationToken ct)
    {
        // Only stored files have a blob key; a free-text note or external id is just forgotten.
        if (!reference.StartsWith("kadari/", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            await blobs.DeleteAsync(reference, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kadari erasure: blob delete failed for {Key}.", reference);
            return false;
        }
    }
}
