using System.ComponentModel.DataAnnotations;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// Credentials + model for any OpenAI-compatible chat-completions provider — OpenAI, Groq, Mistral,
/// OpenRouter, or a local Ollama — selected by <see cref="BaseUrl"/> and <see cref="Model"/>. The one adapter
/// serves them all; only these three values change between them.
/// </summary>
public sealed class OpenAiVisionOptions
{
    public const string SectionName = "OpenAiVision";

    public const string PlaceholderApiKey = "REPLACE_WITH_YOUR_API_KEY";

    /// <summary>The provider's OpenAI-compatible base, e.g. https://api.openai.com/v1,
    /// https://api.groq.com/openai/v1, https://openrouter.ai/api/v1, http://localhost:11434/v1 (Ollama).</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>A vision-capable model on that provider, e.g. gpt-4o-mini, llama-3.2-90b-vision-preview,
    /// pixtral-12b, qwen2.5vl.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Never commit a real key — use user-secrets or the <c>OpenAiVision__ApiKey</c> env var. A
    /// local Ollama needs no real key; leave any non-placeholder value.</summary>
    public string ApiKey { get; set; } = PlaceholderApiKey;

    [Range(0, 2)]
    public double Temperature { get; set; } = 0.2;

    [Range(256, 32768)]
    public int MaxOutputTokens { get; set; } = 4096;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.Equals(ApiKey, PlaceholderApiKey, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Model);
}
