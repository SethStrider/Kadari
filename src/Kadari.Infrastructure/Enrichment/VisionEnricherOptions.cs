using System.ComponentModel.DataAnnotations;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>Which vision provider drives Kadari enrichment.</summary>
public enum VisionProvider
{
    /// <summary>No model — detections come back unenriched (the keyless default).</summary>
    PassThrough,

    /// <summary>Google Gemini via its own generateContent REST API.</summary>
    Gemini,

    /// <summary>Any OpenAI-compatible chat-completions endpoint — OpenAI, Groq, Mistral, OpenRouter, local
    /// Ollama — selected by base URL + model.</summary>
    OpenAiCompatible,
}

/// <summary>
/// Provider-agnostic enrichment settings (the knobs every provider shares). The provider itself is chosen by
/// <see cref="Provider"/>; provider-specific credentials/models live in <c>GeminiOptions</c> /
/// <c>OpenAiVisionOptions</c>.
/// </summary>
public sealed class VisionEnricherOptions
{
    public const string SectionName = "VisionEnricher";

    public VisionProvider Provider { get; set; } = VisionProvider.PassThrough;

    /// <summary>Ceiling on items returned for one snap.</summary>
    [Range(1, 60)]
    public int MaxItems { get; set; } = 24;

    /// <summary>Sanity ceiling on a single item's replacement value, in whole ZAR.</summary>
    [Range(1, 100_000_000)]
    public long MaxReplacementValueMajor { get; set; } = 10_000_000;

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>Caps in-flight calls (each holds an image in memory).</summary>
    [Range(1, 32)]
    public int MaxConcurrentCalls { get; set; } = 4;
}
