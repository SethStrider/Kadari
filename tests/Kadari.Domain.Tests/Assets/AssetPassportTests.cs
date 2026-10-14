using Kadari.Domain.Assets;
using Kadari.Domain.Assets.Events;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Domain.Tests.Assets;

public class AssetPassportTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Asset IdentifiedTv()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Samsung 65\" TV", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.Observed("device", "blob://still.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.97, Now));
        return asset;
    }

    [Fact]
    public void Mint_snapshots_the_assets_identity_assurance_and_owner()
    {
        var asset = IdentifiedTv();

        var passport = AssetPassport.Mint(asset, "AP-ABC123", Now);

        Assert.Equal(asset.Id, passport.AssetId);
        Assert.Equal(asset.OwnerId, passport.OwnerId);
        Assert.Equal(asset.TenantId, passport.TenantId);
        Assert.Equal("AP-ABC123", passport.VerificationId);
        Assert.Equal("Samsung 65\" TV", passport.DisplayName);
        Assert.Equal(AssetCategory.Electronics, passport.Category);
        Assert.Equal(AssetAssuranceLevel.Identified, passport.Assurance);
        Assert.Equal(2, passport.EvidenceCount);
        Assert.Equal(AssetPassportStatus.Active, passport.Status);
        Assert.True(passport.IsValid);
    }

    [Fact]
    public void Mint_snapshots_the_best_replacement_value()
    {
        var asset = IdentifiedTv();
        asset.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(1_500_00), Now));

        var passport = AssetPassport.Mint(asset, "AP-VALUE", Now);

        Assert.NotNull(passport.ReplacementValue);
        Assert.Equal(1_500_00, passport.ReplacementValue!.AmountMinor);
    }

    [Fact]
    public void Mint_raises_AssetPassportMinted()
    {
        var asset = IdentifiedTv();

        var passport = AssetPassport.Mint(asset, "AP-EVT", Now);

        var minted = Assert.Single(passport.DomainEvents.OfType<AssetPassportMintedDomainEvent>());
        Assert.Equal(passport.Id, minted.PassportId);
        Assert.Equal(asset.Id, minted.AssetId);
        Assert.Equal(asset.OwnerId, minted.OwnerId);
    }

    [Fact]
    public void Mint_rejects_a_blank_verification_id()
    {
        var asset = IdentifiedTv();

        Assert.Throws<ArgumentException>(() => AssetPassport.Mint(asset, "   ", Now));
    }

    [Fact]
    public void Refresh_re_snapshots_as_evidence_accrues()
    {
        var asset = IdentifiedTv();
        var passport = AssetPassport.Mint(asset, "AP-REFRESH", Now);

        asset.Attach(AssetEvidence.OwnershipSupported("receipt", "blob://receipt.pdf", Now));
        asset.Attach(AssetEvidence.ValueSupported("receipt", Money.Zar(2_000_00), "blob://receipt.pdf", Now));
        passport.Refresh(asset, Now.AddDays(1));

        Assert.Equal(AssetAssuranceLevel.ValueSupported, passport.Assurance);
        Assert.Equal(2_000_00, passport.ReplacementValue!.AmountMinor);
        Assert.Equal(4, passport.EvidenceCount);
        Assert.Equal(Now.AddDays(1), passport.LastUpdatedAt);
    }

    [Fact]
    public void Refresh_ignores_an_asset_that_is_not_this_passports()
    {
        var asset = IdentifiedTv();
        var passport = AssetPassport.Mint(asset, "AP-MINE", Now);

        var other = IdentifiedTv();
        other.Attach(AssetEvidence.ValueSupported("receipt", Money.Zar(9_999_00), "blob://x.pdf", Now));
        passport.Refresh(other, Now.AddDays(1));

        Assert.Equal(AssetAssuranceLevel.Identified, passport.Assurance);
        Assert.Null(passport.ReplacementValue);
    }

    [Fact]
    public void Refresh_is_frozen_once_revoked()
    {
        var asset = IdentifiedTv();
        var passport = AssetPassport.Mint(asset, "AP-REVOKED", Now);
        passport.Revoke(Now.AddDays(1));

        asset.Attach(AssetEvidence.ValueSupported("receipt", Money.Zar(2_000_00), "blob://receipt.pdf", Now));
        passport.Refresh(asset, Now.AddDays(2));

        Assert.Equal(AssetPassportStatus.Revoked, passport.Status);
        Assert.Null(passport.ReplacementValue);
        Assert.False(passport.IsValid);
    }

    [Fact]
    public void Suspend_makes_the_credential_no_longer_valid()
    {
        var passport = AssetPassport.Mint(IdentifiedTv(), "AP-SUSPEND", Now);

        passport.Suspend(Now.AddDays(1));

        Assert.Equal(AssetPassportStatus.Suspended, passport.Status);
        Assert.False(passport.IsValid);
    }
}
