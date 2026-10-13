using System.Text.Json;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;
using Kadari.Domain.ValueObjects;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// Turns a model's answer into identified assets (ported from PoziSnap's <c>EnrichmentReplyMapper</c>,
/// adapted for Kadari). Separated from the call so the part that can be wrong on its own is testable
/// without a network, a key or a model. Everything here treats the reply as hostile input: indexes that do
/// not exist, boxes that are inverted or outside the frame, values that are a hallucinated eight figures,
/// the same seed claimed twice.
/// </summary>
public static class AssetEnrichmentReplyMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Gemini emits detection boxes on a 0-1000 grid.</summary>
    private const double BoxScale = 1000d;

    /// <summary>A box thinner than this is a sliver nothing can be cropped from.</summary>
    private const double MinBoxEdge = 0.01;

    /// <summary>What a model-found item's confidence reads as when it gives none.</summary>
    private const double DefaultFoundConfidence = 0.7;

    private const int MaxDisplayNameLength = 120;
    private const int MaxConditionLength = 40;
    private const int MaxSerialHintLength = 80;

    /// <summary>
    /// Maps a reply onto the detections it was given, in ZAR.
    /// </summary>
    /// <returns>The identified items, or null when the reply yielded nothing usable — the caller falls back
    /// to the raw detections rather than showing an empty snap.</returns>
    public static IReadOnlyList<IdentifiedItem>? Map(
        string? json,
        IReadOnlyList<DetectedObject> detections,
        int maxItems,
        long maxReplacementMajor)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        AssetEnrichmentReply? reply;
        try
        {
            reply = JsonSerializer.Deserialize<AssetEnrichmentReply>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (reply?.Items is null || reply.Items.Count == 0)
        {
            return null;
        }

        var items = new List<IdentifiedItem>(reply.Items.Count);

        // One seed can only become one item. A model that claims index 0 twice gets its second claim
        // treated as a find with no box, which is then dropped.
        var claimed = new HashSet<int>();

        foreach (var candidate in reply.Items)
        {
            if (candidate.Skip || items.Count >= maxItems)
            {
                continue;
            }

            var mapped = MapItem(candidate, detections, claimed, maxReplacementMajor);
            if (mapped is not null)
            {
                items.Add(mapped);
            }
        }

        return items.Count > 0 ? items : null;
    }

    private static IdentifiedItem? MapItem(
        AssetEnrichmentReplyItem candidate,
        IReadOnlyList<DetectedObject> detections,
        HashSet<int> claimed,
        long maxReplacementMajor)
    {
        var seedIndex = SeedIndexOf(candidate, detections, claimed);
        DetectedObject? seed = null;

        if (seedIndex is not null)
        {
            claimed.Add(seedIndex.Value);
            seed = detections[seedIndex.Value];
        }

        // The device's box wins wherever there is one — it measured pixels; the model is describing a photo.
        var box = seed is null ? ParseBox(candidate.Box) : null;
        if (seed is null && box is null)
        {
            return null;
        }

        var name = Trim(candidate.DisplayName, MaxDisplayNameLength);

        // A found item has no detector label to fall back on, so its name is the only one it will ever have.
        if (seed is null && name is null)
        {
            return null;
        }

        return new IdentifiedItem(
            Label: seed?.Label ?? name!,
            Confidence: seed?.Confidence ?? Confidence(candidate.Confidence),
            X: seed?.X ?? box!.Value.X,
            Y: seed?.Y ?? box!.Value.Y,
            Width: seed?.Width ?? box!.Value.Width,
            Height: seed?.Height ?? box!.Value.Height,
            DisplayName: name ?? seed!.Label,
            Category: ParseCategory(candidate.Category),
            Condition: Trim(candidate.Condition, MaxConditionLength),
            SerialHint: Trim(candidate.SerialHint, MaxSerialHintLength),
            ReplacementValue: ToMoney(candidate.ReplacementValue, maxReplacementMajor),
            SourceIndex: seedIndex);
    }

    private static int? SeedIndexOf(
        AssetEnrichmentReplyItem candidate, IReadOnlyList<DetectedObject> detections, HashSet<int> claimed)
    {
        var index = candidate.SeedIndex;
        return index is null || index < 0 || index >= detections.Count || claimed.Contains(index.Value)
            ? null
            : index;
    }

    private static (double X, double Y, double Width, double Height)? ParseBox(List<double>? box)
    {
        if (box is not { Count: 4 } || box.Any(double.IsNaN))
        {
            return null;
        }

        var top = Clamp01(box[0] / BoxScale);
        var left = Clamp01(box[1] / BoxScale);
        var bottom = Clamp01(box[2] / BoxScale);
        var right = Clamp01(box[3] / BoxScale);

        // Models occasionally emit the pair the other way round; ordering the edges rescues that.
        var (x, width) = left <= right ? (left, right - left) : (right, left - right);
        var (y, height) = top <= bottom ? (top, bottom - top) : (bottom, top - bottom);

        return width < MinBoxEdge || height < MinBoxEdge ? null : (x, y, width, height);
    }

    private static AssetCategory ParseCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return AssetCategory.Other;
        }

        var value = raw.Trim();
        if (Enum.TryParse<AssetCategory>(value, ignoreCase: true, out var exact))
        {
            return exact;
        }

        var lower = value.ToLowerInvariant();
        if (lower.Contains("art") || lower.Contains("collect") || lower.Contains("antique"))
        {
            return AssetCategory.ArtAndCollectibles;
        }

        return lower switch
        {
            "electronic" or "electronics" or "appliance" or "appliances" or "tech" => AssetCategory.Electronics,
            "furniture" or "furnishing" or "furnishings" => AssetCategory.Furniture,
            "clothing" or "clothes" or "apparel" or "garment" => AssetCategory.Clothing,
            "jewellery" or "jewelry" or "watch" or "watches" => AssetCategory.Jewellery,
            _ => AssetCategory.Other,
        };
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    private static double Confidence(double? value) =>
        value is null || double.IsNaN(value.Value) ? DefaultFoundConfidence : Math.Clamp(value.Value, 0, 1);

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    /// <summary>Whole ZAR to a <see cref="Money"/> in cents, rejecting non-positive or hallucinated values.</summary>
    private static Money? ToMoney(double? major, long maxMajor) =>
        major is null || double.IsNaN(major.Value) || major <= 0 || major > maxMajor
            ? null
            : Money.Zar((long)Math.Round(major.Value * 100));
}
