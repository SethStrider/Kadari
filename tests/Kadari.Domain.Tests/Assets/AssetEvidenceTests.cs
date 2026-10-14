using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Assets.Events;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Domain.Tests.Assets;

public class AssetEvidenceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Asset AnAsset() =>
        Asset.Register(TenantId.Default, OwnerId.New(), "Samsung 65\" TV", AssetCategory.Electronics, Now);

    [Fact]
    public void Observed_is_a_self_asserted_image()
    {
        var e = AssetEvidence.Observed("device", "blob/frame-1.jpg", Now);

        Assert.Equal(AssetEvidenceKind.Image, e.Kind);
        Assert.Equal(EvidenceStrength.SelfAsserted, e.Strength);
        Assert.Equal("blob/frame-1.jpg", e.Reference);
    }

    [Fact]
    public void Identified_carries_confidence_but_stays_self_asserted()
    {
        var e = AssetEvidence.Identified("gemini", 0.97, Now);

        Assert.Equal(AssetEvidenceKind.Identification, e.Kind);
        Assert.Equal(EvidenceStrength.SelfAsserted, e.Strength);
        Assert.Equal(0.97, e.Confidence);
    }

    [Fact]
    public void Identified_rejects_confidence_outside_zero_to_one()
    {
        Assert.Throws<DomainException>(() => AssetEvidence.Identified("gemini", 1.4, Now));
    }

    [Fact]
    public void ProfessionallyValued_is_an_independent_value_sourced_to_the_valuer()
    {
        var e = AssetEvidence.ProfessionallyValued("Acme Valuers", Money.Zar(80_000_00), "cert/AV-2026-001", Now);

        Assert.Equal(AssetEvidenceKind.Value, e.Kind);
        Assert.Equal(EvidenceStrength.Independent, e.Strength);
        Assert.Equal("Acme Valuers", e.Source);
        Assert.Equal("cert/AV-2026-001", e.Reference);
        Assert.Equal(Money.Zar(80_000_00), e.Amount);
    }

    [Fact]
    public void A_professional_valuation_lifts_an_ownership_supported_asset_to_value_supported()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", "blob/receipt.pdf", Now));
        Assert.Equal(AssetAssuranceLevel.OwnershipSupported, asset.Assurance);

        asset.Attach(AssetEvidence.ProfessionallyValued("Acme Valuers", Money.Zar(80_000_00), "cert/AV-1", Now));

        Assert.Equal(AssetAssuranceLevel.ValueSupported, asset.Assurance);
    }

    [Fact]
    public void A_professional_valuation_without_ownership_stays_identified()
    {
        var asset = AnAsset();
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));

        asset.Attach(AssetEvidence.ProfessionallyValued("Acme Valuers", Money.Zar(80_000_00), "cert/AV-1", Now));

        // Independent value alone does not reach ValueSupported — the ladder still needs independent ownership.
        Assert.Equal(AssetAssuranceLevel.Identified, asset.Assurance);
    }

    [Fact]
    public void OwnershipSupported_is_independent()
    {
        var e = AssetEvidence.OwnershipSupported("receipt", "blob/receipt.pdf", Now);

        Assert.Equal(AssetEvidenceKind.Ownership, e.Kind);
        Assert.Equal(EvidenceStrength.Independent, e.Strength);
    }

    [Fact]
    public void ValueEstimated_is_a_self_asserted_value_with_an_amount()
    {
        var e = AssetEvidence.ValueEstimated("gemini", Money.Zar(2_749_900), Now);

        Assert.Equal(AssetEvidenceKind.Value, e.Kind);
        Assert.Equal(EvidenceStrength.SelfAsserted, e.Strength);
        Assert.Equal(Money.Zar(2_749_900), e.Amount);
    }

    [Fact]
    public void ValueSupported_is_independent_and_carries_the_source_artefact()
    {
        var e = AssetEvidence.ValueSupported("valuer", Money.Zar(3_000_000), "blob/valuation.pdf", Now);

        Assert.Equal(AssetEvidenceKind.Value, e.Kind);
        Assert.Equal(EvidenceStrength.Independent, e.Strength);
        Assert.Equal("blob/valuation.pdf", e.Reference);
    }

    [Fact]
    public void Attach_adds_to_the_evidence_set_and_raises_the_event()
    {
        var asset = AnAsset();

        var attached = asset.Attach(AssetEvidence.Identified("gemini", 0.91, Now));

        Assert.Contains(attached, asset.Evidence);
        var evt = Assert.Single(asset.DomainEvents.OfType<AssetEvidenceAttachedDomainEvent>());
        Assert.Equal(attached.Id, evt.EvidenceId);
        Assert.Equal(AssetEvidenceKind.Identification, evt.Kind);
    }

    [Fact]
    public void Evidence_is_append_only_and_preserves_order()
    {
        var asset = AnAsset();

        var image = asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        var id = asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));

        Assert.Equal([image.Id, id.Id], asset.Evidence.Select(e => e.Id));
    }
}
