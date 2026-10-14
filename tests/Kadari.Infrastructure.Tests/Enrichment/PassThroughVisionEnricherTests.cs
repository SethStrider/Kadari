using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Infrastructure.Enrichment;

namespace Kadari.Infrastructure.Tests.Enrichment;

public class PassThroughVisionEnricherTests
{
    [Fact]
    public async Task Hands_the_detections_back_unenriched_with_their_indexes()
    {
        var request = new VisionEnrichmentRequest(
            Image: null!,
            Detections: [new DetectedObject("tv", 0.9, 0, 0, 1, 1), new DetectedObject("sofa", 0.8, 0, 0, 1, 1)],
            RoomLabel: "Living room");

        var result = await new PassThroughVisionEnricher().EnrichAsync(request, CancellationToken.None);

        Assert.False(result.Enriched);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal([0, 1], result.Items.Select(i => i.SourceIndex));
        Assert.Equal("tv", result.Items[0].DisplayName);
    }
}
