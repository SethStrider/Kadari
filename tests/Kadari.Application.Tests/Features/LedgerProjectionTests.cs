using Kadari.Application.Features.GetLedger;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Tests.Features;

public class LedgerProjectionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly Guid Customer = Guid.NewGuid();

    private static Asset AssetIn(string room, string name, AssetCategory category = AssetCategory.Electronics) =>
        Asset.Register(TenantId.Default, OwnerId.New(), name, category, Now, roomLabel: room);

    [Fact]
    public void Empty_ledger_is_zeroed_with_the_default_currency()
    {
        var ledger = LedgerProjection.From(Customer, []);

        Assert.Equal(0, ledger.AssetCount);
        Assert.Equal(0, ledger.RecordedReplacementExposureMinor);
        Assert.Equal(0, ledger.CompletenessPercent);
        Assert.Equal("ZAR", ledger.Currency);
        Assert.Empty(ledger.Rooms);
    }

    [Fact]
    public void Assets_group_by_room_with_per_room_totals()
    {
        var tv = AssetIn("Living room", "TV");
        tv.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(1000), Now));
        var couch = AssetIn("Living room", "Couch", AssetCategory.Furniture);
        couch.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(2000), Now));
        var bed = AssetIn("Bedroom", "Bed", AssetCategory.Furniture);
        bed.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(500), Now));

        var ledger = LedgerProjection.From(Customer, [tv, couch, bed]);

        Assert.Equal(3, ledger.AssetCount);
        Assert.Equal(3500, ledger.RecordedReplacementExposureMinor);
        var living = ledger.Rooms.Single(r => r.Room == "Living room");
        Assert.Equal(3000, living.RoomTotalMinor);
        Assert.Equal(2, living.Assets.Count);
    }

    [Fact]
    public void An_asset_with_no_room_is_grouped_under_Unassigned()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Loose item", AssetCategory.Other, Now);

        var ledger = LedgerProjection.From(Customer, [asset]);

        Assert.Equal(LedgerProjection.UnassignedRoom, ledger.Rooms.Single().Room);
    }

    [Fact]
    public void Best_value_prefers_independent_over_estimate()
    {
        var tv = AssetIn("Living room", "TV");
        tv.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(1000), Now));
        tv.Attach(AssetEvidence.ValueSupported("valuer", Money.Zar(1500), "blob/val.pdf", Now));

        var ledger = LedgerProjection.From(Customer, [tv]);

        Assert.Equal(1500, ledger.RecordedReplacementExposureMinor);
        Assert.Equal(1500, ledger.Rooms.Single().Assets.Single().BestValueMinor);
    }

    [Fact]
    public void Completeness_is_the_fraction_of_assets_that_have_a_value()
    {
        var valued = AssetIn("Living room", "TV");
        valued.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(1000), Now));
        var unvalued = AssetIn("Living room", "Lamp");   // identified but never valued

        var ledger = LedgerProjection.From(Customer, [valued, unvalued]);

        Assert.Equal(50, ledger.CompletenessPercent);
    }

    [Fact]
    public void Asset_dto_carries_the_derived_assurance_and_status()
    {
        var tv = AssetIn("Living room", "TV");
        tv.Attach(AssetEvidence.Observed("device", "blob/f.jpg", Now));
        tv.Attach(AssetEvidence.Identified("gemini", 0.9, Now));

        var dto = LedgerProjection.From(Customer, [tv]).Rooms.Single().Assets.Single();

        Assert.Equal(nameof(AssetAssuranceLevel.Identified), dto.Assurance);
        Assert.Equal(nameof(AssetLifecycleStatus.Active), dto.Status);
        Assert.Null(dto.BestValueMinor);
    }
}
