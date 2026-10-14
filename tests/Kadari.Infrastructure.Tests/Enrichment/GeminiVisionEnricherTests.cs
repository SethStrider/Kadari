using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;
using Kadari.Infrastructure.Enrichment;

namespace Kadari.Infrastructure.Tests.Enrichment;

/// <summary>
/// Exercises the Gemini adapter without a network or a key: a stub handler stands in for the API, so the
/// request-building, response-parsing (via the mapper) and the graceful fallback are all provable offline.
/// </summary>
public class GeminiVisionEnricherTests
{
    private static readonly DetectedObject Tv = new("tv", 0.94, 0.1, 0.2, 0.3, 0.4);

    private static GeminiVisionEnricher Enricher(HttpStatusCode status, string body)
    {
        var factory = new SingleClientFactory(new HttpClient(new StubHandler(status, body)));
        var vision = Options.Create(new VisionEnricherOptions());
        var gemini = Options.Create(new GeminiOptions { ApiKey = "test-key" });
        return new GeminiVisionEnricher(factory, vision, gemini, new VisionConcurrencyGate(vision), NullLogger<GeminiVisionEnricher>.Instance);
    }

    private static VisionEnrichmentRequest ARequest() => new(FakeImage(), [Tv], "Living room");

    [Fact]
    public async Task A_successful_reply_is_mapped_to_enriched_items()
    {
        const string reply = """
        {"candidates":[{"content":{"parts":[{"text":
          "{\"items\":[{\"seedIndex\":0,\"displayName\":\"Samsung 65\\\" TV\",\"category\":\"Electronics\",\"replacementValue\":27499}]}"
        }]}}]}
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
        var result = await Enricher(HttpStatusCode.InternalServerError, "boom").EnrichAsync(ARequest(), CancellationToken.None);

        Assert.False(result.Enriched);
        var item = Assert.Single(result.Items);
        Assert.Equal("tv", item.DisplayName);   // the raw detection, unenriched
        Assert.Equal(0, item.SourceIndex);
    }

    [Fact]
    public async Task An_unparseable_body_falls_back_to_raw_detections()
    {
        var result = await Enricher(HttpStatusCode.OK, "not the envelope").EnrichAsync(ARequest(), CancellationToken.None);

        Assert.False(result.Enriched);
        Assert.Single(result.Items);
    }

    [Fact]
    public void ExtractText_pulls_the_reply_out_of_the_envelope()
    {
        const string envelope = """{"candidates":[{"content":{"parts":[{"text":"hello"}]}}]}""";

        Assert.Equal("hello", GeminiVisionEnricher.ExtractText(envelope));
        Assert.Null(GeminiVisionEnricher.ExtractText("{}"));
        Assert.Null(GeminiVisionEnricher.ExtractText(null));
    }

    private static SnapImage FakeImage()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, [1, 2, 3]);
        return new SnapImage(path, "image/jpeg", 3);
    }
}
