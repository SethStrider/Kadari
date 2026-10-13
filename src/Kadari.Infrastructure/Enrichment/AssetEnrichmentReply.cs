using System.Text.Json.Serialization;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// The shape the model is asked to answer in (adapted from PoziSnap for Kadari identification). Every
/// field is nullable and nothing is trusted — this is model output; <see cref="AssetEnrichmentReplyMapper"/>
/// is where all of it is checked, which is why this type holds no logic.
/// </summary>
internal sealed record AssetEnrichmentReply
{
    [JsonPropertyName("items")]
    public List<AssetEnrichmentReplyItem>? Items { get; init; }
}

internal sealed record AssetEnrichmentReplyItem
{
    /// <summary>The on-device detection this is, by index — or null/-1 when the model found it itself.</summary>
    [JsonPropertyName("seedIndex")]
    public int? SeedIndex { get; init; }

    /// <summary><c>[ymin, xmin, ymax, xmax]</c>, each 0-1000 (Gemini's detection convention). Required for an
    /// item with no seed; ignored for one that has a seed (the device's box is the more accurate).</summary>
    [JsonPropertyName("box")]
    public List<double>? Box { get; init; }

    /// <summary>The item name — make/model where the model can tell (e.g. "Samsung 65\" Neo QLED TV").</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>One of the Kadari categories; mapped to the enum (unknown ⇒ Other).</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Condition read from the photo (e.g. "good", "worn"), free text.</summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; init; }

    /// <summary>A serial/model number if legible in the image — a hint, never trusted as verified.</summary>
    [JsonPropertyName("serialHint")]
    public string? SerialHint { get; init; }

    /// <summary>Estimated current <b>replacement</b> value in whole ZAR — the cost to buy an equivalent new,
    /// not resale (deck §17).</summary>
    [JsonPropertyName("replacementValue")]
    public double? ReplacementValue { get; init; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }

    /// <summary>Set when the model judged the thing not an insurable household asset.</summary>
    [JsonPropertyName("skip")]
    public bool Skip { get; init; }
}
