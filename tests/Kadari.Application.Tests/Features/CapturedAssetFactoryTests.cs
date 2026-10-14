using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.Valuation;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Tests.Features;

public class CapturedAssetFactoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static IdentifiedItem AnItem(Money? value = null, double confidence = 0.9) => new(
        Label: "tv", Confidence: confidence, X: 0, Y: 0, Width: 1, Height: 1,
        DisplayName: "Samsung 65\" TV", Category: AssetCategory.Electronics,
        Condition: "good", SerialHint: null, ReplacementValue: value, SourceIndex: 0);

    [Fact]
    public void Create_registers_the_asset_with_image_and_identification_evidence()
    {
        var asset = CapturedAssetFactory.Create(
            TenantId.Default, OwnerId.New(), "blob/snap.img", "gemini", AnItem(), valuation: null, Now, "Living room");

        Assert.Equal("Samsung 65\" TV", asset.DisplayName);
        Assert.Equal(AssetCategory.Electronics, asset.Category);
        Assert.Equal("Living room", asset.RoomLabel);
        Assert.Contains(asset.Evidence, e => e.Kind == AssetEvidenceKind.Image && e.Reference == "blob/snap.img");
        Assert.Contains(asset.Evidence, e => e.Kind == AssetEvidenceKind.Identification);
        Assert.Equal(AssetAssuranceLevel.Identified, asset.Assurance);
    }

    [Fact]
    public void Create_without_a_valuation_attaches_no_value_evidence()
    {
        var asset = CapturedAssetFactory.Create(
            TenantId.Default, OwnerId.New(), "blob/snap.img", "gemini", AnItem(), valuation: null, Now, null);

        Assert.DoesNotContain(asset.Evidence, e => e.Kind == AssetEvidenceKind.Value);
    }

    [Fact]
    public void Create_with_a_self_asserted_valuation_stays_below_ValueSupported()
    {
        var valuation = new Valuation(Money.Zar(2_749_900), EvidenceStrength.SelfAsserted, "ai-baseline");

        var asset = CapturedAssetFactory.Create(
            TenantId.Default, OwnerId.New(), "blob/snap.img", "gemini", AnItem(), valuation, Now, null);

        var value = asset.Evidence.Single(e => e.Kind == AssetEvidenceKind.Value);
        Assert.Equal(EvidenceStrength.SelfAsserted, value.Strength);
        Assert.Equal("ai-baseline", value.Source);
        Assert.Equal(Money.Zar(2_749_900), value.Amount);
        Assert.Equal(AssetAssuranceLevel.Identified, asset.Assurance);   // an AI estimate never lifts to ValueSupported
    }

    [Fact]
    public void Create_with_an_independent_valuation_attaches_supported_value_evidence()
    {
        var valuation = new Valuation(Money.Zar(3_000_000), EvidenceStrength.Independent, "valuer", "blob/cert.pdf");

        var asset = CapturedAssetFactory.Create(
            TenantId.Default, OwnerId.New(), "blob/snap.img", "gemini", AnItem(), valuation, Now, null);

        var value = asset.Evidence.Single(e => e.Kind == AssetEvidenceKind.Value);
        Assert.Equal(EvidenceStrength.Independent, value.Strength);
        Assert.Equal("blob/cert.pdf", value.Reference);
    }

    [Fact]
    public void Create_clamps_an_out_of_range_confidence()
    {
        // A model returning a slightly-off confidence should not throw the whole capture.
        var asset = CapturedAssetFactory.Create(
            TenantId.Default, OwnerId.New(), "blob/snap.img", "gemini", AnItem(confidence: 1.3), valuation: null, Now, null);

        var identification = asset.Evidence.Single(e => e.Kind == AssetEvidenceKind.Identification);
        Assert.Equal(1.0, identification.Confidence);
    }
}
