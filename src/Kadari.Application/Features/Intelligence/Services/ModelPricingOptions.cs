namespace Kadari.Application.Features.Intelligence.Services;

/// <summary>What one million tokens cost for a model, in <see cref="ModelPricingOptions.Currency"/>.</summary>
public sealed class ModelPrice
{
    public decimal InputPerMillionTokens { get; set; }
    public decimal OutputPerMillionTokens { get; set; }
}

/// <summary>
/// Model prices, bound from <c>ModelPricing</c> — e.g. <c>ModelPricing:Models:gemini-3.5-flash-lite:InputPerMillionTokens</c>.
/// A model with no price still has its tokens recorded; only its cost is left empty.
/// </summary>
public sealed class ModelPricingOptions
{
    public const string SectionName = "ModelPricing";

    public string Currency { get; set; } = "USD";

    public Dictionary<string, ModelPrice> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
