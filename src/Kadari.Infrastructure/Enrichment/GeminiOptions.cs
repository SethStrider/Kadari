using System.ComponentModel.DataAnnotations;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// Configuration for the Gemini vision enricher (adapted from PoziSnap for Kadari). Absent/placeholder
/// key ⇒ the capture flow still runs end to end via the pass-through enricher, so a checked-out repo works
/// without a key.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public const string PlaceholderApiKey = "REPLACE_WITH_YOUR_GEMINI_API_KEY";

    /// <summary>Never commit a real key — use user-secrets or the <c>Gemini__ApiKey</c> env var.</summary>
    public string ApiKey { get; set; } = PlaceholderApiKey;

    /// <summary>The appraiser model. See PoziSnap's notes: the cheapest tier, allowed to think.</summary>
    public string ModelId { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>"none"/"low"/"medium"/"high" (Gemini 3.x). When set it wins over <see cref="ThinkingBudget"/>.</summary>
    public string? ThinkingLevel { get; set; } = "medium";

    [Range(0, 24576)]
    public int ThinkingBudget { get; set; } = 4096;

    [Range(256, 32768)]
    public int MaxOutputTokens { get; set; } = 8192;

    [Range(256, 32768)]
    public int AnswerTokenHeadroom { get; set; } = 3072;

    public int EffectiveThinkingBudget =>
        Math.Clamp(Math.Min(ThinkingBudget, MaxOutputTokens - AnswerTokenHeadroom), 0, 24576);

    /// <summary>Appraisal wants the model's best single answer, not variety.</summary>
    [Range(0, 2)]
    public double Temperature { get; set; } = 0.2;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.Equals(ApiKey, PlaceholderApiKey, StringComparison.Ordinal);
}
