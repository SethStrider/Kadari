using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.Services;

/// <summary>How the owner sends the pack: the provider's own channel, opened with the message ready.</summary>
public sealed record CaseHandoffDto(string ChannelType, string? Target, string? Link);

/// <summary>What the owner reviews before sending (CARE/14 C10): each fact that will leave KADARI, and the message.</summary>
public sealed record CasePackDto(IReadOnlyList<SharedFieldDto> Shared, string Message, CaseHandoffDto? Handoff, string TermsVersion, string Terms);

/// <summary>
/// The case pack (CARE/14 C10 "minimum necessary personal data"): the product, the purchase, the problem, the
/// references so far, and only the evidence the route asks for. No name, email or number — the owner sends it from
/// their own email or WhatsApp, so the provider already has those. The authorisation records the same list.
/// </summary>
public static class CasePack
{
    /// <summary>The wording the owner agrees to. Bump the version whenever the wording changes.</summary>
    public const string TermsVersion = "care-case-handoff-v1";

    public const string Terms =
        "I am sending these details to the provider named, through their own channel, for this problem only. KADARI records " +
        "what was shared.";

    public static CasePackDto For(Asset asset, AssetCase @case)
    {
        var facts = asset.LatestPurchase?.Purchase;
        var serial = asset.Evidence
            .Where(e => e.Kind == AssetEvidenceKind.Ownership && e.Source == "serial" && e.Reference != null)
            .OrderByDescending(e => e.AssertedAt)
            .Select(e => e.Reference)
            .FirstOrDefault();
        var receiptHeld = asset.Evidence.Any(e => e.Kind == AssetEvidenceKind.Ownership && e.Source == "receipt" && e.Reference != null);

        var shared = new List<SharedFieldDto>
        {
            new("Brand", "Brand", facts?.Brand),
            new("Model", "Model", facts?.Model),
            new("Issue", "The problem", @case.IssueSummary),
        };
        if (facts?.PurchasedOn is { } purchased)
        {
            shared.Add(new SharedFieldDto("PurchaseDate", "Purchase date", purchased.ToString("yyyy-MM-dd")));
        }

        if (!string.IsNullOrWhiteSpace(facts?.Retailer))
        {
            shared.Add(new SharedFieldDto("Retailer", "Bought from", facts.Retailer));
        }

        if (@case.RequiredEvidence.Contains(RouteEvidence.SerialNumber))
        {
            shared.Add(new SharedFieldDto("SerialNumber", "Serial number", serial));
        }

        if (@case.RequiredEvidence.Contains(RouteEvidence.ProofOfPurchase))
        {
            shared.Add(new SharedFieldDto("ProofOfPurchase", "Proof of purchase", receiptHeld ? "Attach your receipt — KADARI holds a copy" : null));
        }

        foreach (var reference in @case.References)
        {
            shared.Add(new SharedFieldDto("Reference", $"{reference.Party} reference", reference.Reference));
        }

        var message = Message(@case, shared);
        return new CasePackDto(shared, message, Handoff(@case, message), TermsVersion, Terms);
    }

    /// <summary>One line per known fact — a plain message the owner can paste anywhere.</summary>
    private static string Message(AssetCase @case, IReadOnlyList<SharedFieldDto> shared)
    {
        var lines = new List<string> { $"Hello{(@case.TargetName is null ? "" : $" {@case.TargetName}")},", "", "I need help with a product:" };
        lines.AddRange(shared.Where(s => s.Field != "Issue" && !string.IsNullOrWhiteSpace(s.Value)).Select(s => $"- {s.Label}: {s.Value}"));
        lines.Add("");
        lines.Add("What's wrong:");
        lines.Add(@case.IssueSummary);
        lines.Add("");
        lines.Add("Please let me know the next step and a reference number.");
        return string.Join('\n', lines);
    }

    private static CaseHandoffDto? Handoff(AssetCase @case, string message)
    {
        if (@case.ChannelType is not { } type)
        {
            return null;
        }

        var value = @case.ChannelValue;
        var subject = Uri.EscapeDataString("Product support request");
        var body = Uri.EscapeDataString(message);
        var link = type switch
        {
            ContactChannelType.WebPage => value,
            ContactChannelType.Email when value is not null => $"mailto:{value}?subject={subject}&body={body}",
            ContactChannelType.WhatsApp when value is not null => $"https://wa.me/{new string(value.Where(char.IsAsciiDigit).ToArray())}?text={body}",
            ContactChannelType.Phone when value is not null => $"tel:{new string(value.Where(c => char.IsAsciiDigit(c) || c == '+').ToArray())}",
            _ => null,
        };
        return new CaseHandoffDto(type.ToString(), value, link);
    }
}
