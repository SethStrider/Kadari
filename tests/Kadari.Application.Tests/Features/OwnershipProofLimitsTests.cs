using Kadari.Application.Features.RecordOwnershipProof;
using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;

namespace Kadari.Application.Tests.Features;

/// <summary>AS-13/14: ownership proof is bounded, and serials, currency and amounts are checked.</summary>
public class OwnershipProofLimitsTests
{
    private static bool Valid(string? serial, long? amountMinor = null, string? currency = null) =>
        new RecordOwnershipProofValidator()
            .Validate(new RecordOwnershipProofCommand(Guid.NewGuid(), Guid.NewGuid(), null, serial, amountMinor, currency))
            .IsValid;

    [Theory]
    [InlineData("SN: ab-123 456", true)]
    [InlineData("ab", false)]                         // too short to be a serial
    [InlineData("--- ///", false)]                    // nothing serial-like left
    public void A_serial_needs_three_to_sixty_four_letters_or_digits(string serial, bool valid)
    {
        Assert.Equal(valid, Valid(serial));
    }

    [Fact]
    public void An_over_long_serial_is_refused_rather_than_failing_to_store()
    {
        Assert.False(Valid(new string('A', SerialNumbers.MaxLength + 1)));
    }

    [Theory]
    [InlineData("AB123456")]
    [InlineData("ab-123 456")]
    [InlineData("SN: ab-123 456")]
    [InlineData("S/N AB/123/456")]
    [InlineData("Serial No. ab123456")]
    public void Serials_normalise_to_one_spelling(string typed)
    {
        Assert.Equal("AB123456", SerialNumbers.Normalise(typed));
    }

    [Fact]
    public void A_serial_that_starts_with_sn_keeps_its_letters()
    {
        Assert.Equal("SN12345", SerialNumbers.Normalise("sn12345"));
    }

    [Fact]
    public void An_asset_holds_at_most_ten_proofs_of_ownership()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "MacBook Air", AssetCategory.Electronics, DateTimeOffset.UnixEpoch);
        for (var i = 0; i < Asset.MaxOwnershipProofs; i++)
        {
            asset.Attach(AssetEvidence.OwnershipSupported("serial", $"SERIAL{i:000}", DateTimeOffset.UnixEpoch));
        }

        Assert.Throws<DomainException>(() =>
            asset.Attach(AssetEvidence.OwnershipSupported("serial", "ONEMORE", DateTimeOffset.UnixEpoch)));
    }
}
