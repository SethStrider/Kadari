using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;

namespace Kadari.Domain.Tests.Assets;

public class PurchaseEvidenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static Asset AnIdentifiedAsset()
    {
        var asset = Asset.Register(TenantId.Default, Owners.OwnerId.New(), "Bosch washing machine", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.Observed("capture", "img", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        return asset;
    }

    [Fact]
    public void PurchaseRecorded_DoesNotMoveTheAssuranceLadder()
    {
        var asset = AnIdentifiedAsset();

        asset.Attach(AssetEvidence.PurchaseRecorded("customer", new PurchaseFacts("Bosch", null, "Makro", new DateOnly(2026, 3, 14)), Now, "receipt-key"));

        Assert.Equal(AssetAssuranceLevel.Identified, asset.Assurance);
    }

    [Fact]
    public void PurchaseRecorded_WithAReceipt_IsIndependent()
    {
        var evidence = AssetEvidence.PurchaseRecorded("customer", new PurchaseFacts("Bosch", null, null, null), Now, "receipt-key");

        Assert.Equal(EvidenceStrength.Independent, evidence.Strength);
    }

    [Fact]
    public void LatestPurchase_IsTheNewestCorrection()
    {
        var asset = AnIdentifiedAsset();
        asset.Attach(AssetEvidence.PurchaseRecorded("customer", new PurchaseFacts("Bosh", null, null, null), Now));

        asset.Attach(AssetEvidence.PurchaseRecorded("customer", new PurchaseFacts("Bosch", null, null, null), Now.AddMinutes(1)));

        Assert.Equal("Bosch", asset.LatestPurchase!.Purchase!.Brand);
    }

    [Fact]
    public void PurchaseFacts_WithNothingInThem_Throw()
        => Assert.Throws<DomainException>(() => new PurchaseFacts(" ", null, "", null));
}
