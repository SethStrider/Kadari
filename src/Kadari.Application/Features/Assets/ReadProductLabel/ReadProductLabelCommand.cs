using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Assets.ReadProductLabel;

/// <summary>
/// Read a photo of the product's rating plate, serial label or box with AI and propose its brand, model and serial
/// number (CARE/14 C3) — so the owner never has to type a serial the camera can read (CARE/05 §5). The photo is not
/// stored: confirming the serial records it as evidence through the proof endpoint, and the brand and model through
/// <c>RecordPurchaseDetails</c>. Location metadata is stripped before the model sees it. Customer-scoped.
/// </summary>
/// <remarks>A command, not a query: the model call is recorded (usage, cost, quota). The endpoint owns the photo's
/// lifetime.</remarks>
public sealed record ReadProductLabelCommand(Guid CustomerId, Guid AssetId, SnapImage Photo)
    : ICommand<PurchaseProposalDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class ReadProductLabelHandler(IKadariDbContext db, OwnedAssets assets, PurchaseReader reader)
    : ICommandHandler<ReadProductLabelCommand, PurchaseProposalDto>
{
    public async Task<PurchaseProposalDto> Handle(ReadProductLabelCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetAsync(cmd.CustomerId, cmd.AssetId, ct);

        await cmd.Photo.StripMetadataAsync(ct);
        byte[] content;
        await using (var stream = cmd.Photo.OpenRead())
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }

        var proposal = await reader.ReadAsync(
            cmd, PurchaseExtraction.LabelFeature, PurchaseExtraction.LabelPrompt(asset.DisplayName),
            new ModelAttachment(cmd.Photo.ContentType, content), cmd.CustomerId, ct);

        await db.SaveChangesAsync(ct);   // the usage row
        return proposal;
    }
}
