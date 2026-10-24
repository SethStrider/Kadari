using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Common.Files;
using Kadari.Application.Features.Assets.Services;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Assets.ReadReceipt;

/// <summary>
/// Read the asset's stored receipt with AI and propose the purchase details on it — retailer, date, brand, model,
/// price — each with its confidence (CARE/14 C3). Nothing is saved but the model's usage: the owner checks the
/// proposal and confirms it through <c>RecordPurchaseDetails</c>. Reads the receipt KADARI already holds (the newest),
/// so the document is never uploaded twice. Customer-scoped.
/// </summary>
/// <remarks>A command, not a query: the model call is recorded (usage, cost, quota).</remarks>
public sealed record ReadReceiptCommand(Guid CustomerId, Guid AssetId) : ICommand<PurchaseProposalDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class ReadReceiptHandler(IKadariDbContext db, OwnedAssets assets, IBlobStorage blobs, PurchaseReader reader)
    : ICommandHandler<ReadReceiptCommand, PurchaseProposalDto>
{
    /// <summary>The model gets at most this much document (a phone photo or a short PDF).</summary>
    private const int MaxDocumentBytes = 12 * 1024 * 1024;

    public async Task<PurchaseProposalDto> Handle(ReadReceiptCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetAsync(cmd.CustomerId, cmd.AssetId, ct);
        var receiptKey = asset.Evidence
            .Where(e => e.Kind == AssetEvidenceKind.Ownership && e.Source == "receipt" && e.Reference != null)
            .OrderByDescending(e => e.AssertedAt)
            .Select(e => e.Reference)
            .FirstOrDefault();

        if (receiptKey is null)
        {
            return PurchaseProposalDto.Nothing(ExtractionUnavailable.NoReceipt);
        }

        byte[] content;
        await using (var stream = await blobs.DownloadAsync(receiptKey, ct))
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }

        var kind = FileSignature.Detect(content.AsSpan(0, Math.Min(content.Length, FileSignature.HeaderLength)));
        var contentType = kind == FileKind.Pdf ? "application/pdf" : FileSignature.ImageContentType(kind);
        if (contentType is null || content.Length > MaxDocumentBytes)
        {
            return PurchaseProposalDto.Nothing(ExtractionUnavailable.UnsupportedDocument);
        }

        var proposal = await reader.ReadAsync(
            cmd, PurchaseExtraction.ReceiptFeature, PurchaseExtraction.ReceiptPrompt(asset.DisplayName),
            new ModelAttachment(contentType, content), cmd.CustomerId, ct);

        await db.SaveChangesAsync(ct);   // the usage row
        return proposal;
    }
}
