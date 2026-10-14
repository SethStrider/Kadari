using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Features.RecordOwnershipProof;

/// <summary>
/// Record independent proof of ownership for one of a customer's assets (deck §16, D4/D10) — a receipt
/// document and/or a serial number, optionally with the receipt's stated price. Unlike the self-asserted
/// evidence a capture produces, these are <see cref="EvidenceStrength.Independent"/>, so they lift the asset
/// up the assurance ladder: independent ownership → OwnershipSupported, and a receipt-backed value →
/// ValueSupported. Customer-scoped — a caller may only prove ownership of their own asset.
/// </summary>
/// <remarks>The endpoint owns the <see cref="ReceiptDocument"/> lifetime and disposes it after this returns.</remarks>
public sealed record RecordOwnershipProofCommand(
    Guid CustomerId,
    Guid AssetId,
    ReceiptDocument? Receipt,
    string? SerialNumber,
    long? AmountMinor,
    string? Currency) : ICommand<RecordOwnershipProofResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record RecordOwnershipProofResponse(Guid AssetId, string Assurance);

internal sealed class RecordOwnershipProofHandler(
    IKadariDbContext db, IBlobStorage blobs, TimeProvider clock)
    : ICommandHandler<RecordOwnershipProofCommand, RecordOwnershipProofResponse>
{
    private const string ReceiptSource = "receipt";
    private const string SerialSource = "serial";

    public async Task<RecordOwnershipProofResponse> Handle(RecordOwnershipProofCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);

        var subjectId = await db.Owners.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (OwnerId?)s.Id)
            .FirstOrDefaultAsync(ct);

        var assetId = new AssetId(cmd.AssetId);
        var asset = subjectId is null
            ? null
            : await db.Assets
                .Include(a => a.Evidence)
                .FirstOrDefaultAsync(a => a.Id == assetId && a.OwnerId == subjectId.Value, ct);

        if (asset is null)
        {
            throw new NotFoundException(nameof(Asset), cmd.AssetId);
        }

        var now = clock.GetUtcNow();

        // NOTE (CLAUDE.md §6a): the blob upload is external I/O in a command handler, so a transient-DB retry
        // re-runs it — an accepted at-least-once hazard (re-upload overwrites the same key). Migrate to a
        // post-commit integration event if it bites.
        string? receiptKey = null;
        if (cmd.Receipt is not null)
        {
            receiptKey = $"kadari/{cmd.CustomerId:N}/receipts/{Guid.CreateVersion7():N}.doc";
            await cmd.Receipt.StripMetadataAsync(ct);   // a photographed receipt carries GPS too (AS-06)
            await using var stream = cmd.Receipt.OpenRead();
            await blobs.UploadAsync(receiptKey, stream, cmd.Receipt.ContentType, ct);
            asset.Attach(AssetEvidence.OwnershipSupported(ReceiptSource, receiptKey, now));
        }

        if (!string.IsNullOrWhiteSpace(cmd.SerialNumber))
        {
            // Stored normalised (AS-14), so the same device reads the same on every ledger.
            asset.Attach(AssetEvidence.OwnershipSupported(SerialSource, SerialNumbers.Normalise(cmd.SerialNumber)!, now));
        }

        // A receipt-backed price is an independently-supported value — it needs the receipt as its reference,
        // which the validator guarantees is present whenever an amount is given.
        if (cmd.AmountMinor is { } minor && receiptKey is not null)
        {
            var money = new Money(minor, string.IsNullOrWhiteSpace(cmd.Currency) ? "ZAR" : cmd.Currency!);
            asset.Attach(AssetEvidence.ValueSupported(ReceiptSource, money, receiptKey, now));
        }

        await db.SaveChangesAsync(ct);

        return new RecordOwnershipProofResponse(asset.Id.Value, asset.Assurance.ToString());
    }
}
