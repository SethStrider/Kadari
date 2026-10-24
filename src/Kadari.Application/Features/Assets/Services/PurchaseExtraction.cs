using System.Globalization;
using System.Text.Json;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Assets.Services;

/// <summary>One value the model read, with how sure it said it was (0–1). A proposal — never a fact until the owner
/// confirms it.</summary>
public sealed record ProposedValueDto(string Value, double Confidence);

/// <summary>A price the model read, in minor units.</summary>
public sealed record ProposedAmountDto(long AmountMinor, string Currency, double Confidence);

/// <summary>
/// What KADARI read off a receipt or a product label (CARE/06 §3, CARE/14 C3), for the owner to check and confirm.
/// <see cref="Read"/> is false with an <see cref="Unavailable"/> reason when nothing could be read — the owner simply
/// types the details instead; extraction never blocks manual entry.
/// </summary>
public sealed record PurchaseProposalDto(
    bool Read,
    string? Unavailable,
    ProposedValueDto? Brand,
    ProposedValueDto? Model,
    ProposedValueDto? Retailer,
    ProposedValueDto? PurchasedOn,
    ProposedValueDto? SerialNumber,
    ProposedAmountDto? Price)
{
    public static PurchaseProposalDto Nothing(string reason) => new(false, reason, null, null, null, null, null, null);
}

/// <summary>Why extraction could not produce a proposal (the codes the client shows plain words for).</summary>
public static class ExtractionUnavailable
{
    public const string NotConfigured = "NotConfigured";
    public const string NoCrossBorderConsent = "NoCrossBorderConsent";
    public const string QuotaReached = "QuotaReached";
    public const string NoReceipt = "NoReceipt";
    public const string UnsupportedDocument = "UnsupportedDocument";
    public const string NothingRead = "NothingRead";
}

/// <summary>
/// The prompts for reading receipts and product labels, and the parser for the model's reply. The reply is untrusted
/// model output: every field is optional, bounded and checked here — an unparseable date, an implausible price or an
/// over-long string is dropped, never repaired. A serial number is only trimmed, never altered to fit a pattern
/// (CARE/06 §3.2).
/// </summary>
public static class PurchaseExtraction
{
    public const string ReceiptFeature = "care-receipt";
    public const string LabelFeature = "care-label";

    /// <summary>R5,000,000 in cents — the same ceiling as a receipt amount (AS-14).</summary>
    private const long MaxPriceMinor = 500_000_000;

    public const string System = """
        You read South African purchase documents and product labels for a household's asset record.
        Reply with one JSON object only. For every field give {"value": ..., "confidence": 0.0-1.0}, or null when the
        field is not clearly visible. Never guess, infer or complete a value that is not printed: a missing field is
        null. Copy serial and model numbers exactly as printed, character for character.
        """;

    public static string ReceiptPrompt(string assetName) => $$"""
        This is the receipt or invoice for: "{{assetName}}".
        Return {"retailer": ..., "purchaseDate": ..., "brand": ..., "model": ..., "price": ...} where purchaseDate is
        yyyy-MM-dd, price is the amount paid for this item in rand as a number (not the receipt total unless it is
        the only item), and brand/model are for this item's line only.
        """;

    public static string LabelPrompt(string assetName) => $$"""
        This is a photo of the rating plate, serial label or box of: "{{assetName}}".
        Return {"brand": ..., "model": ..., "serialNumber": ...}.
        """;

    public static PurchaseProposalDto Parse(string? reply, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return PurchaseProposalDto.Nothing(ExtractionUnavailable.NothingRead);
        }

        try
        {
            using var document = JsonDocument.Parse(StripFence(reply));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return PurchaseProposalDto.Nothing(ExtractionUnavailable.NothingRead);
            }

            var proposal = new PurchaseProposalDto(
                Read: true,
                Unavailable: null,
                Brand: Text(root, "brand", PurchaseFacts.MaxTextLength),
                Model: Text(root, "model", PurchaseFacts.MaxTextLength),
                Retailer: Text(root, "retailer", PurchaseFacts.MaxTextLength),
                PurchasedOn: Date(root, "purchaseDate", today),
                SerialNumber: Serial(root),
                Price: Price(root));

            var readAnything = proposal.Brand is not null || proposal.Model is not null || proposal.Retailer is not null
                               || proposal.PurchasedOn is not null || proposal.SerialNumber is not null || proposal.Price is not null;
            return readAnything ? proposal : PurchaseProposalDto.Nothing(ExtractionUnavailable.NothingRead);
        }
        catch (JsonException)
        {
            return PurchaseProposalDto.Nothing(ExtractionUnavailable.NothingRead);
        }
    }

    private static ProposedValueDto? Text(JsonElement root, string name, int maxLength)
    {
        if (!TryField(root, name, out var value, out var confidence) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > maxLength ? null : new ProposedValueDto(text, confidence);
    }

    private static ProposedValueDto? Serial(JsonElement root)
    {
        var serial = Text(root, "serialNumber", SerialNumbers.MaxLength);
        return serial is not null && serial.Value.Count(char.IsLetterOrDigit) >= 3 ? serial : null;
    }

    private static ProposedValueDto? Date(JsonElement root, string name, DateOnly today)
    {
        var text = Text(root, name, 10);
        return text is not null
               && DateOnly.TryParseExact(text.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
               && date <= today && date.Year >= 1950
            ? text
            : null;
    }

    private static ProposedAmountDto? Price(JsonElement root)
    {
        if (!TryField(root, "price", out var value, out var confidence))
        {
            return null;
        }

        decimal amount;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            amount = number;
        }
        else if (value.ValueKind == JsonValueKind.String
                 && decimal.TryParse(value.GetString()?.Replace("R", "").Replace(" ", "").Replace(",", ""),
                     NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            amount = parsed;
        }
        else
        {
            return null;
        }

        var minor = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
        return minor is > 0 and <= MaxPriceMinor ? new ProposedAmountDto(minor, "ZAR", confidence) : null;
    }

    /// <summary>A field is {"value": x, "confidence": c}; a bare value is accepted with confidence 0.5 (unknown).</summary>
    private static bool TryField(JsonElement root, string name, out JsonElement value, out double confidence)
    {
        value = default;
        confidence = 0.5;
        if (!root.TryGetProperty(name, out var field) || field.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (field.ValueKind == JsonValueKind.Object)
        {
            if (!field.TryGetProperty("value", out value) || value.ValueKind == JsonValueKind.Null)
            {
                return false;
            }

            if (field.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var reported))
            {
                confidence = Math.Clamp(reported, 0, 1);
            }

            return true;
        }

        value = field;
        return true;
    }

    private static string StripFence(string reply)
    {
        var trimmed = reply.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine > 0 && lastFence > firstLine ? trimmed[(firstLine + 1)..lastFence] : trimmed;
    }
}
