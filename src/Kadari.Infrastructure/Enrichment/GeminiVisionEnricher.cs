using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Infrastructure.Enrichment;

/// <summary>
/// Identifies and values the household assets in a snap using Gemini over its generateContent REST API
/// (a plain <see cref="HttpClient"/>, to avoid an alpha SDK dependency). The shared gate/timeout/fallback/
/// mapping live in <see cref="VisionEnricherBase"/>; this supplies only the Gemini call.
/// </summary>
public sealed class GeminiVisionEnricher(
    IHttpClientFactory httpClientFactory,
    IOptions<VisionEnricherOptions> visionOptions,
    IOptions<GeminiOptions> geminiOptions,
    VisionConcurrencyGate gate,
    ILogger<GeminiVisionEnricher> logger) : VisionEnricherBase(gate, visionOptions, logger)
{
    public const string HttpClientName = "kadari-gemini";

    /// <summary>Gemini processes the still on Google infrastructure outside SA — a cross-border disclosure.</summary>
    public override bool IsCrossBorder => true;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly GeminiOptions _gemini = geminiOptions.Value;

    protected override async Task<string?> CallModelAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        var base64 = await ReadImageBase64Async(request, cancellationToken).ConfigureAwait(false);

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = AssetEnrichmentPrompt.System } } },
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = AssetEnrichmentPrompt.ForRequest(request, Options.MaxItems) },
                        new { inlineData = new { mimeType = request.Image.ContentType, data = base64 } },
                    },
                },
            },
            generationConfig = new
            {
                temperature = _gemini.Temperature,
                maxOutputTokens = _gemini.MaxOutputTokens,
                responseMimeType = "application/json",
                thinkingConfig = ThinkingConfig(_gemini),
            },
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{_gemini.ModelId}:generateContent")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        // The key travels as a header, never in the URL.
        httpRequest.Headers.Add("x-goog-api-key", _gemini.ApiKey);

        using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ExtractText(payload);
    }

    /// <summary>Pulls the model's reply text out of the generateContent envelope.</summary>
    public static string? ExtractText(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            return null;
        }
    }

    // 2.5 models take a token budget; 3.x take a level. Sending both is an error, so a level wins.
    private static object ThinkingConfig(GeminiOptions options) =>
        string.IsNullOrWhiteSpace(options.ThinkingLevel)
            ? new { thinkingBudget = options.EffectiveThinkingBudget }
            : new { thinkingLevel = options.ThinkingLevel.Trim() };
}
