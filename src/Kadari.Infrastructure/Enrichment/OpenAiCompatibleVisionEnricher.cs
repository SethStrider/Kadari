using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// Identifies and values household assets using any OpenAI-compatible chat-completions endpoint — OpenAI,
/// Groq, Mistral, OpenRouter, or a local Ollama — selected purely by base URL + model. The image goes up as
/// a base64 data URL in an <c>image_url</c> part; the reply text (which the prompt constrains to JSON) is fed
/// to the shared mapper. Gate/timeout/fallback come from <see cref="VisionEnricherBase"/>.
/// </summary>
public sealed class OpenAiCompatibleVisionEnricher(
    IHttpClientFactory httpClientFactory,
    IOptions<VisionEnricherOptions> visionOptions,
    IOptions<OpenAiVisionOptions> providerOptions,
    VisionConcurrencyGate gate,
    ILogger<OpenAiCompatibleVisionEnricher> logger) : VisionEnricherBase(gate, visionOptions, logger)
{
    public const string HttpClientName = "kadari-openai";

    /// <summary>A hosted OpenAI-compatible provider processes the still off-shore — a cross-border disclosure.
    /// (A purely local Ollama deployment is the operator's call to reconfigure; the safe default is true.)</summary>
    public override bool IsCrossBorder => true;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly OpenAiVisionOptions _provider = providerOptions.Value;

    protected override async Task<string?> CallModelAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        var base64 = await ReadImageBase64Async(request, cancellationToken).ConfigureAwait(false);
        var dataUrl = $"data:{request.Image.ContentType};base64,{base64}";

        var body = new
        {
            model = _provider.Model,
            temperature = _provider.Temperature,
            max_tokens = _provider.MaxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = AssetEnrichmentPrompt.System },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = AssetEnrichmentPrompt.ForRequest(request, Options.MaxItems) },
                        new { type = "image_url", image_url = new { url = dataUrl } },
                    },
                },
            },
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_provider.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _provider.ApiKey);

        using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ExtractContent(payload);
    }

    /// <summary>Pulls the assistant message text out of the chat-completions envelope.</summary>
    public static string? ExtractContent(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            return null;
        }
    }
}
