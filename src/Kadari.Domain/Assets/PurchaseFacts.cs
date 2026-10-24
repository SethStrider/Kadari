using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Assets;

/// <summary>
/// What was bought, where and when — the inputs KADARI Care's coverage engine reads. A value object owned by a
/// <see cref="AssetEvidenceKind.Purchase"/> evidence item. Every field is optional because capture is
/// scan-first, not a form: a brand alone already narrows the registry lookup.
/// </summary>
public sealed record PurchaseFacts
{
    public const int MaxTextLength = 120;

    private PurchaseFacts() { }   // EF

    public PurchaseFacts(string? brand, string? model, string? retailer, DateOnly? purchasedOn)
    {
        Brand = Clean(brand);
        Model = Clean(model);
        Retailer = Clean(retailer);
        PurchasedOn = purchasedOn;

        if (Brand is null && Model is null && Retailer is null && PurchasedOn is null)
        {
            throw new DomainException("Purchase details need at least one fact.");
        }
    }

    public string? Brand { get; private init; }
    public string? Model { get; private init; }
    public string? Retailer { get; private init; }
    public DateOnly? PurchasedOn { get; private init; }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > MaxTextLength
            ? throw new DomainException($"A purchase detail is at most {MaxTextLength} characters.")
            : trimmed;
    }
}
