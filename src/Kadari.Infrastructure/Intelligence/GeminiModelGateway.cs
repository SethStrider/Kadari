using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Infrastructure.Enrichment;

namespace Kadari.Infrastructure.Intelligence;

/// <summary>
/// Gemini over its generateContent REST API (a plain <see cref="HttpClient"/>, to avoid an alpha SDK dependency). Takes
/// images and PDFs inline, asks for a JSON reply, and reads the token counts from <c>usageMetadata</c> (thinking tokens
/// are billed as output).
/// </summary>
public sealed class GeminiModelGateway(
    IHttpClientFactory httpClientFactory,
    IOptions<GeminiOptions> geminiOptions,
    VisionConcurrencyGate gate,
    IOptions<VisionEnricherOptions> options,
    ILogger<GeminiModelGateway> logger) : ModelGatewayBase(gate, options, logger)
{
    public const string HttpClientName = "kadari-gemini";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly GeminiOptions _gemini = geminiOptions.Value;

    public override bool IsConfigured => _gemini.IsConfigured;

    protected override string Provider => "gemini";

    protected override string Model => _gemini.ModelId;

    protected override bool Accepts(string contentType) =>
        base.Accepts(contentType) || string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    protected override async Task<(string? Text, ModelCallUsage? Usage)> SendAsync(ModelCall call, CancellationToken cancellationToken)
    {
        var parts = new List<object> { new { text = call.UserPrompt } };
        if (call.Attachment is { } attachment)
        {
            parts.Add(new { inlineData = new { mimeType = attachment.ContentType, data = Convert.ToBase64String(attachment.Content) } });
        }

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = call.SystemPrompt } } },
            contents = new[] { new { parts } },
            generationConfig = new
            {
                temperature = _gemini.Temperature,
                maxOutputTokens = _gemini.MaxOutputTokens,
                responseMimeType = "application/json",
                thinkingConfig = ThinkingConfig(_gemini),
            },
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{_gemini.ModelId}:generateContent")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        // The key travels as a header, never in the URL.
        request.Headers.Add("x-goog-api-key", _gemini.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (ExtractText(payload), ExtractUsage(payload, _gemini.ModelId));
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

    /// <summary>Reads the token counts from <c>usageMetadata</c>; thinking tokens count as output.</summary>
    public static ModelCallUsage ExtractUsage(string? payload, string model)
    {
        int? input = null, output = null;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.TryGetProperty("usageMetadata", out var meta))
                {
                    input = Int(meta, "promptTokenCount");
                    var candidates = Int(meta, "candidatesTokenCount");
                    var thoughts = Int(meta, "thoughtsTokenCount");
                    output = candidates is null && thoughts is null ? null : (candidates ?? 0) + (thoughts ?? 0);
                }
            }
            catch (JsonException)
            {
            }
        }

        return new ModelCallUsage("gemini", model, input, output);
    }

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    // 2.5 models take a token budget; 3.x take a level. Sending both is an error, so a level wins.
    private static object ThinkingConfig(GeminiOptions options) =>
        string.IsNullOrWhiteSpace(options.ThinkingLevel)
            ? new { thinkingBudget = options.EffectiveThinkingBudget }
            : new { thinkingLevel = options.ThinkingLevel.Trim() };
}
