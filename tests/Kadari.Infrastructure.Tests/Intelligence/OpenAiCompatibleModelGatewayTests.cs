using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;
using Kadari.Infrastructure.Enrichment;
using Kadari.Infrastructure.Intelligence;
using Kadari.Infrastructure.Tests.Enrichment;

namespace Kadari.Infrastructure.Tests.Intelligence;

/// <summary>
/// The OpenAI-compatible adapter (Groq / OpenAI / Mistral / OpenRouter / Ollama) proven offline via a stub
/// handler: chat-completions envelope parsed through the shared mapper, and graceful fallback on failure.
/// </summary>
public class OpenAiCompatibleModelGatewayTests
{
    private static readonly DetectedObject Tv = new("tv", 0.94, 0.1, 0.2, 0.3, 0.4);

    private static OpenAiCompatibleModelGateway Gateway(HttpStatusCode status, string body)
    {
        var factory = new SingleClientFactory(new HttpClient(new StubHandler(status, body)));
        var vision = Options.Create(new VisionEnricherOptions());
        var provider = Options.Create(new OpenAiVisionOptions { ApiKey = "test-key", BaseUrl = "https://api.groq.com/openai/v1", Model = "llama-vision" });
        return new OpenAiCompatibleModelGateway(factory, provider, new VisionConcurrencyGate(vision), vision, NullLogger<OpenAiCompatibleModelGateway>.Instance);
    }

    private static ModelVisionEnricher Enricher(HttpStatusCode status, string body) =>
        new(Gateway(status, body), Options.Create(new VisionEnricherOptions()), NullLogger<ModelVisionEnricher>.Instance);

    [Fact]
    public async Task A_pdf_attachment_is_refused_without_calling_the_provider()
    {
        var reply = await Gateway(HttpStatusCode.OK, "{}").CompleteAsync(
            new Kadari.Application.Abstractions.Intelligence.ModelCall(
                "test", "system", "user", new Kadari.Application.Abstractions.Intelligence.ModelAttachment("application/pdf", [1])),
            CancellationToken.None);

        Assert.Equal((false, (Kadari.Application.Abstractions.Intelligence.ModelCallUsage?)null), (reply.Answered, reply.Usage));
    }

    [Fact]
    public void ExtractUsage_reads_prompt_and_completion_tokens()
    {
        var usage = OpenAiCompatibleModelGateway.ExtractUsage("""{"usage":{"prompt_tokens":900,"completion_tokens":45}}""", "llama-vision");

        Assert.Equal((900, 45), (usage.InputTokens, usage.OutputTokens));
    }

    private static VisionEnrichmentRequest ARequest() => new(FakeImage(), [Tv], "Living room");

    [Fact]
    public async Task A_successful_reply_is_mapped_to_enriched_items()
    {
        const string reply = """
        {"choices":[{"message":{"role":"assistant","content":
          "{\"items\":[{\"seedIndex\":0,\"displayName\":\"Samsung 65\\\" TV\",\"category\":\"Electronics\",\"replacementValue\":27499}]}"
        }}]}
        """;

        var result = await Enricher(HttpStatusCode.OK, reply).EnrichAsync(ARequest(), CancellationToken.None);

        Assert.True(result.Enriched);
        var item = Assert.Single(result.Items);
        Assert.Equal("Samsung 65\" TV", item.DisplayName);
        Assert.Equal(AssetCategory.Electronics, item.Category);
    }

    [Fact]
    public async Task An_api_error_falls_back_to_raw_detections()
    {
        var result = await Enricher(HttpStatusCode.Unauthorized, "no").EnrichAsync(ARequest(), CancellationToken.None);

        Assert.False(result.Enriched);
        Assert.Equal("tv", Assert.Single(result.Items).DisplayName);
    }

    [Fact]
    public void ExtractContent_pulls_the_message_out_of_the_envelope()
    {
        const string envelope = """{"choices":[{"message":{"role":"assistant","content":"hello"}}]}""";

        Assert.Equal("hello", OpenAiCompatibleModelGateway.ExtractContent(envelope));
        Assert.Null(OpenAiCompatibleModelGateway.ExtractContent("{}"));
        Assert.Null(OpenAiCompatibleModelGateway.ExtractContent(null));
    }

    private static SnapImage FakeImage()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, [1, 2, 3]);
        return new SnapImage(path, "image/jpeg", 3);
    }
}
