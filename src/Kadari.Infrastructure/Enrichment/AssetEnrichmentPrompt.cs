using System.Text;
using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// Builds the instruction the vision model answers. Separated from the call so the wording — the part most
/// likely to need tuning — is one readable place. Kadari asks for insurance identification and a
/// <b>replacement</b> value, not a PoziSnap resale listing.
/// </summary>
public static class AssetEnrichmentPrompt
{
    public const string System =
        "You are an insurance asset appraiser building a home-contents inventory from a photograph. " +
        "Identify every insurable household asset visible in the image. For each, give the make/model where " +
        "legible, one category, the condition, any serial or model number you can read, and an estimated " +
        "current REPLACEMENT value in South African Rand — the cost to buy an equivalent NEW item today, " +
        "not a resale or depreciated price. Do not include people, pets, food, plants, or building fixtures " +
        "(walls, built-in cupboards, light fittings). Respond ONLY with JSON — no prose, no markdown.";

    /// <summary>The per-request user message: the room, the device hints, and the exact answer shape.</summary>
    public static string ForRequest(VisionEnrichmentRequest request, int maxItems)
    {
        var sb = new StringBuilder();

        sb.Append("Return at most ").Append(maxItems).Append(" items as a JSON object with an 'items' array. ");
        sb.Append("Each item has these fields: ");
        sb.Append("seedIndex (integer index of the on-device hint you are reusing, or null if you found it yourself); ");
        sb.Append("box (four numbers [ymin, xmin, ymax, xmax] on a 0-1000 grid; required only when seedIndex is null); ");
        sb.Append("displayName (string, the make/model where legible); ");
        sb.Append("category (one of: Electronics, Furniture, Clothing, Jewellery, ArtAndCollectibles, Other); ");
        sb.Append("condition (string); serialHint (string or null); ");
        sb.Append("replacementValue (number, whole South African Rand to buy an equivalent new item today); ");
        sb.Append("confidence (number 0-1); skip (boolean, true for anything that is not an insurable household asset). ");

        if (!string.IsNullOrWhiteSpace(request.RoomLabel))
        {
            sb.Append("The room is: ").Append(request.RoomLabel).Append(". ");
        }

        if (request.Detections.Count > 0)
        {
            sb.Append("On-device detection hints (reuse a hint by its seedIndex and keep its box): ");
            for (var i = 0; i < request.Detections.Count; i++)
            {
                var d = request.Detections[i];
                sb.Append('[').Append(i).Append("]=").Append(d.Label).Append(' ');
            }
        }
        else
        {
            sb.Append("There are no on-device hints; find the assets yourself and give each a box. ");
        }

        return sb.ToString();
    }
}
