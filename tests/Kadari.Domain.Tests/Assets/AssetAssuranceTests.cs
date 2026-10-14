using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Domain.Tests.Assets;

public class AssetAssuranceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Asset AnAsset() =>
        Asset.Register(TenantId.Default, OwnerId.New(), "Samsung 65\" TV", AssetCategory.Electronics, Now);

    [Fact]
    public void No_evidence_is_None()
    {
        Assert.Equal(AssetAssuranceLevel.None, AnAsset().Assurance);
    }

    [Fact]
    public void An_image_alone_is_Observed()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));

        Assert.Equal(AssetAssuranceLevel.Observed, asset.Assurance);
    }

    [Fact]
    public void A_high_confidence_identification_alone_only_reaches_Identified()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.97, Now));

        // 97% is recognition confidence, not assurance — it does not lift past Identified (D4).
        Assert.Equal(AssetAssuranceLevel.Identified, asset.Assurance);
    }

    [Fact]
    public void Identification_without_an_image_does_not_reach_Identified()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Identified("gemini", 0.97, Now));

        Assert.Equal(AssetAssuranceLevel.None, asset.Assurance);
    }

    [Fact]
    public void Independent_ownership_lifts_to_OwnershipSupported()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", "blob/receipt.pdf", Now));

        Assert.Equal(AssetAssuranceLevel.OwnershipSupported, asset.Assurance);
    }

    [Fact]
    public void An_ai_value_estimate_does_not_reach_ValueSupported()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", "blob/receipt.pdf", Now));
        asset.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(2_749_900), Now));

        // Self-asserted value is a displayed number, not assurance (D10).
        Assert.Equal(AssetAssuranceLevel.OwnershipSupported, asset.Assurance);
    }

    [Fact]
    public void Independent_value_reaches_ValueSupported()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", "blob/receipt.pdf", Now));
        asset.Attach(AssetEvidence.ValueSupported("valuer", Money.Zar(3_000_000), "blob/valuation.pdf", Now));

        Assert.Equal(AssetAssuranceLevel.ValueSupported, asset.Assurance);
    }

    [Fact]
    public void Independent_value_without_ownership_does_not_skip_to_ValueSupported()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        asset.Attach(AssetEvidence.ValueSupported("valuer", Money.Zar(3_000_000), "blob/valuation.pdf", Now));

        // The ladder is cumulative — no independent ownership yet, so it holds at Identified.
        Assert.Equal(AssetAssuranceLevel.Identified, asset.Assurance);
    }
}
