using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Enrichment;

namespace Kadari.Infrastructure.Tests.Enrichment;

/// <summary>
/// The reply mapper treats model output as hostile input (ported from PoziSnap). No network, key or model
/// — just JSON in, identified items out.
/// </summary>
public class AssetEnrichmentReplyMapperTests
{
    private const long MaxMajor = 10_000_000;

    private static readonly IReadOnlyList<DetectedObject> OneTv =
        [new DetectedObject("tv", 0.94, X: 0.1, Y: 0.2, Width: 0.3, Height: 0.4)];

    private static IReadOnlyList<IdentifiedItem>? Map(string json, IReadOnlyList<DetectedObject>? detections = null) =>
        AssetEnrichmentReplyMapper.Map(json, detections ?? [], maxItems: 24, MaxMajor);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"items":[]}""")]
    public void Unusable_replies_map_to_null(string? json)
    {
        Assert.Null(Map(json!));
    }

    [Fact]
    public void A_seeded_item_keeps_the_device_box_and_takes_the_enriched_fields()
    {
        var json = """
        {"items":[{"seedIndex":0,"displayName":"Samsung 65\" TV","category":"Electronics",
                   "condition":"good","serialHint":"SN123","replacementValue":27499}]}
        """;

        var item = Assert.Single(Map(json, OneTv)!);

        Assert.Equal(0.1, item.X);              // device box wins
        Assert.Equal(0.3, item.Width);
        Assert.Equal("Samsung 65\" TV", item.DisplayName);
        Assert.Equal(AssetCategory.Electronics, item.Category);
        Assert.Equal("SN123", item.SerialHint);
        Assert.Equal(Money.Zar(2_749_900), item.ReplacementValue);
        Assert.Equal(0, item.SourceIndex);
    }

    [Fact]
    public void A_model_found_item_parses_its_own_box_from_the_0_to_1000_grid()
    {
        // [ymin,xmin,ymax,xmax] = [100,200,500,600] on 0..1000 → x=0.2,y=0.1,w=0.4,h=0.4
        var json = """
        {"items":[{"seedIndex":-1,"box":[100,200,500,600],"displayName":"Floor lamp","category":"Furniture"}]}
        """;

        var item = Assert.Single(Map(json)!);

        Assert.Null(item.SourceIndex);
        Assert.Equal(0.2, item.X, 3);
        Assert.Equal(0.1, item.Y, 3);
        Assert.Equal(0.4, item.Width, 3);
        Assert.Equal(AssetCategory.Furniture, item.Category);
    }

    [Fact]
    public void An_inverted_box_is_rescued_by_ordering_the_edges()
    {
        var json = """{"items":[{"box":[500,600,100,200],"displayName":"Rug"}]}""";

        var item = Assert.Single(Map(json)!);

        Assert.Equal(0.2, item.X, 3);
        Assert.Equal(0.4, item.Width, 3);
    }

    [Fact]
    public void A_skipped_item_is_dropped()
    {
        var json = """{"items":[{"seedIndex":0,"skip":true,"displayName":"not an asset"}]}""";

        Assert.Null(Map(json, OneTv));
    }

    [Fact]
    public void A_found_item_with_no_name_is_dropped()
    {
        var json = """{"items":[{"box":[100,100,500,500]}]}""";

        Assert.Null(Map(json));
    }

    [Fact]
    public void A_hallucinated_value_over_the_ceiling_is_rejected()
    {
        var json = """{"items":[{"seedIndex":0,"displayName":"TV","replacementValue":99999999}]}""";

        var item = Assert.Single(Map(json, OneTv)!);
        Assert.Null(item.ReplacementValue);
    }

    [Fact]
    public void An_unknown_category_falls_back_to_Other()
    {
        var json = """{"items":[{"seedIndex":0,"displayName":"TV","category":"gadget"}]}""";

        var item = Assert.Single(Map(json, OneTv)!);
        Assert.Equal(AssetCategory.Other, item.Category);
    }

    [Fact]
    public void A_second_claim_on_the_same_seed_is_dropped()
    {
        var json = """
        {"items":[{"seedIndex":0,"displayName":"TV"},
                  {"seedIndex":0,"displayName":"TV again"}]}
        """;

        var item = Assert.Single(Map(json, OneTv)!);
        Assert.Equal("TV", item.DisplayName);
    }
}
