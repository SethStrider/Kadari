using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Infrastructure.Enrichment;

namespace Kadari.Infrastructure.Intelligence;

/// <summary>
/// Any OpenAI-compatible chat-completions endpoint — OpenAI, Groq, Mistral, OpenRouter, a local Ollama — selected by
/// base URL + model. Images go up as base64 data URLs; documents (PDF) are not accepted. Token counts come from
/// <c>usage</c>.
/// </summary>
public sealed class OpenAiCompatibleModelGateway(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiVisionOptions> providerOptions,
    VisionConcurrencyGate gate,
    IOptions<VisionEnricherOptions> options,
    ILogger<OpenAiCompatibleModelGateway> logger) : ModelGatewayBase(gate, options, logger)
{
    public const string HttpClientName = "kadari-openai";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly OpenAiVisionOptions _provider = providerOptions.Value;

    public override bool IsConfigured => _provider.IsConfigured;

    protected override string Provider => "openai-compatible";

    protected override string Model => _provider.Model;

    protected override async Task<(string? Text, ModelCallUsage? Usage)> SendAsync(ModelCall call, CancellationToken cancellationToken)
    {
        var content = new List<object> { new { type = "text", text = call.UserPrompt } };
        if (call.Attachment is { } attachment)
        {
            var dataUrl = $"data:{attachment.ContentType};base64,{Convert.ToBase64String(attachment.Content)}";
            content.Add(new { type = "image_url", image_url = new { url = dataUrl } });
        }

        var body = new
        {
            model = _provider.Model,
            temperature = _provider.Temperature,
            max_tokens = _provider.MaxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = call.SystemPrompt },
                new { role = "user", content },
            },
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_provider.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _provider.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (ExtractContent(payload), ExtractUsage(payload, _provider.Model));
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

    /// <summary>Reads <c>usage.prompt_tokens</c> / <c>usage.completion_tokens</c>.</summary>
    public static ModelCallUsage ExtractUsage(string? payload, string model)
    {
        int? input = null, output = null;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.TryGetProperty("usage", out var usage))
                {
                    input = usage.TryGetProperty("prompt_tokens", out var p) && p.TryGetInt32(out var pi) ? pi : null;
                    output = usage.TryGetProperty("completion_tokens", out var c) && c.TryGetInt32(out var ci) ? ci : null;
                }
            }
            catch (JsonException)
            {
            }
        }

        return new ModelCallUsage("openai-compatible", model, input, output);
    }
}
