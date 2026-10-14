using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;

namespace Kadari.Application.Tests.Features;

public class IdentifiedItemTests
{
    [Fact]
    public void FromDetection_carries_the_box_and_label_and_defaults_to_unenriched()
    {
        var detection = new DetectedObject("tv", 0.94, X: 0.1, Y: 0.2, Width: 0.3, Height: 0.4);

        var item = IdentifiedItem.FromDetection(detection, sourceIndex: 2);

        Assert.Equal("tv", item.Label);
        Assert.Equal(0.94, item.Confidence);
        Assert.Equal(0.3, item.Width);
        Assert.Equal("tv", item.DisplayName);
        Assert.Equal(AssetCategory.Other, item.Category);
        Assert.Null(item.ReplacementValue);
        Assert.Equal(2, item.SourceIndex);
    }
}
