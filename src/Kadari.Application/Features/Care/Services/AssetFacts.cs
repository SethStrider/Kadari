using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Care.Services;

/// <summary>Facts Care reads off an asset's evidence, in one place: the latest serial number and whether a receipt is held.</summary>
internal static class AssetFacts
{
    public static string? SerialOf(Asset asset) => asset.Evidence
        .Where(e => e.Kind == AssetEvidenceKind.Ownership && e.Source == "serial" && e.Reference != null)
        .OrderByDescending(e => e.AssertedAt)
        .Select(e => e.Reference)
        .FirstOrDefault();

    public static bool ReceiptHeld(Asset asset) =>
        asset.Evidence.Any(e => e.Kind == AssetEvidenceKind.Ownership && e.Source == "receipt" && e.Reference != null);
}
