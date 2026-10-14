using Kadari.Domain.Assets;
using Kadari.Domain.Assets.Events;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Domain.Tests.Assets;

public class AssetTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Register_starts_the_asset_active_and_owned()
    {
        var owner = OwnerId.New();

        var asset = Asset.Register(TenantId.Default, owner,"Samsung 65\" TV", AssetCategory.Electronics, Now, roomLabel: "Living room");

        Assert.Equal(owner, asset.OwnerId);
        Assert.Equal(AssetLifecycleStatus.Active, asset.Status);
        Assert.Equal("Samsung 65\" TV", asset.DisplayName);
        Assert.Equal(AssetCategory.Electronics, asset.Category);
        Assert.Equal("Living room", asset.RoomLabel);
    }

    [Fact]
    public void Register_stamps_the_tenant()
    {
        var tenant = TenantId.New();

        var asset = Asset.Register(tenant, OwnerId.New(), "MacBook Air", AssetCategory.Electronics, Now);

        Assert.Equal(tenant, asset.TenantId);
    }

    [Fact]
    public void Register_raises_AssetRegistered()
    {
        var owner = OwnerId.New();

        var asset = Asset.Register(TenantId.Default, owner,"MacBook Air", AssetCategory.Electronics, Now);

        var registered = Assert.Single(asset.DomainEvents.OfType<AssetRegisteredDomainEvent>());
        Assert.Equal(asset.Id, registered.AssetId);
        Assert.Equal(owner, registered.OwnerId);
    }

    [Fact]
    public void Register_trims_the_display_name_and_normalises_blank_room_to_null()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "  Smeg fridge  ", AssetCategory.Other, Now, roomLabel: "   ");

        Assert.Equal("Smeg fridge", asset.DisplayName);
        Assert.Null(asset.RoomLabel);
    }

    [Fact]
    public void Register_rejects_a_blank_display_name()
    {
        Assert.Throws<ArgumentException>(
            () => Asset.Register(TenantId.Default, OwnerId.New(), "   ", AssetCategory.Furniture, Now));
    }

    [Fact]
    public void ReportStolen_moves_an_active_asset_to_reported_stolen()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "PlayStation 5", AssetCategory.Electronics, Now);

        asset.ReportStolen(Now.AddDays(1));

        Assert.Equal(AssetLifecycleStatus.ReportedStolen, asset.Status);
        Assert.Contains(asset.DomainEvents, e => e is AssetReportedStolenDomainEvent);
    }

    [Fact]
    public void ReportStolen_when_already_stolen_throws()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "PlayStation 5", AssetCategory.Electronics, Now);
        asset.ReportStolen(Now.AddDays(1));

        Assert.Throws<Kadari.Domain.Abstractions.DomainException>(() => asset.ReportStolen(Now.AddDays(2)));
    }

    [Fact]
    public void Recover_returns_a_stolen_asset_to_active()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "PlayStation 5", AssetCategory.Electronics, Now);
        asset.ReportStolen(Now.AddDays(1));

        asset.Recover(Now.AddDays(3));

        Assert.Equal(AssetLifecycleStatus.Active, asset.Status);
        Assert.Contains(asset.DomainEvents, e => e is AssetRecoveredDomainEvent);
    }

    [Fact]
    public void Recover_when_not_stolen_throws()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "PlayStation 5", AssetCategory.Electronics, Now);

        Assert.Throws<Kadari.Domain.Abstractions.DomainException>(() => asset.Recover(Now.AddDays(1)));
    }

    [Fact]
    public void Transfer_repoints_the_owner_and_raises_the_event()
    {
        var owner = OwnerId.New();
        var recipient = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, owner, "TV", AssetCategory.Electronics, Now);

        asset.Transfer(recipient, Now.AddDays(1));

        Assert.Equal(recipient, asset.OwnerId);
        var transferred = Assert.Single(asset.DomainEvents.OfType<AssetTransferredDomainEvent>());
        Assert.Equal(owner, transferred.From);
        Assert.Equal(recipient, transferred.To);
    }

    [Fact]
    public void Transfer_of_a_stolen_asset_throws()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.ReportStolen(Now.AddDays(1));

        Assert.Throws<Kadari.Domain.Abstractions.DomainException>(() => asset.Transfer(OwnerId.New(), Now.AddDays(2)));
    }

    [Fact]
    public void Transfer_to_the_current_owner_is_a_noop()
    {
        var owner = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, owner, "TV", AssetCategory.Electronics, Now);

        asset.Transfer(owner, Now.AddDays(1));

        Assert.Equal(owner, asset.OwnerId);
        Assert.Empty(asset.DomainEvents.OfType<AssetTransferredDomainEvent>());
    }

    [Fact]
    public void Inherit_repoints_the_owner_even_when_reported_stolen()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.ReportStolen(Now.AddDays(1));
        var heir = OwnerId.New();

        asset.Inherit(heir, Now.AddDays(2));

        Assert.Equal(heir, asset.OwnerId);
        Assert.Contains(asset.DomainEvents, e => e is AssetInheritedDomainEvent);
    }

    [Fact]
    public void AddCoOwner_makes_the_asset_owned_by_the_member_and_raises_the_event()
    {
        var owner = OwnerId.New();
        var member = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, owner, "TV", AssetCategory.Electronics, Now);

        asset.AddCoOwner(member, Now.AddDays(1));

        Assert.True(asset.IsOwnedBy(owner));    // the primary owner still owns it
        Assert.True(asset.IsOwnedBy(member));   // and so does the co-owner
        Assert.Single(asset.CoOwners, c => c.MemberId == member);
        Assert.Contains(asset.DomainEvents, e => e is AssetCoOwnerAddedDomainEvent);
    }

    [Fact]
    public void AddCoOwner_of_the_primary_owner_is_a_noop()
    {
        var owner = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, owner, "TV", AssetCategory.Electronics, Now);

        asset.AddCoOwner(owner, Now.AddDays(1));

        Assert.Empty(asset.CoOwners);
    }

    [Fact]
    public void AddCoOwner_is_idempotent()
    {
        var member = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);

        asset.AddCoOwner(member, Now.AddDays(1));
        asset.AddCoOwner(member, Now.AddDays(2));

        Assert.Single(asset.CoOwners);
    }

    [Fact]
    public void Transfer_clears_the_co_owners()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.AddCoOwner(OwnerId.New(), Now);

        asset.Transfer(OwnerId.New(), Now.AddDays(1));

        Assert.Empty(asset.CoOwners);   // a sale hands over the whole asset — the old household's share ends
    }

    [Fact]
    public void Inherit_keeps_the_co_owners()
    {
        var survivor = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.AddCoOwner(survivor, Now);

        asset.Inherit(OwnerId.New(), Now.AddDays(1));

        Assert.True(asset.IsOwnedBy(survivor));   // a surviving co-owner keeps their share
    }

    [Fact]
    public void RemoveCoOwner_ends_the_membership()
    {
        var member = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.AddCoOwner(member, Now.AddDays(1));

        asset.RemoveCoOwner(member, Now.AddDays(2));

        Assert.False(asset.IsOwnedBy(member));
        Assert.Empty(asset.CoOwners);
        Assert.Contains(asset.DomainEvents, e => e is AssetCoOwnerRemovedDomainEvent);
    }

    [Fact]
    public void Reclassify_changes_the_category()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Untitled painting", AssetCategory.Other, Now);

        asset.Reclassify(AssetCategory.ArtAndCollectibles);

        Assert.Equal(AssetCategory.ArtAndCollectibles, asset.Category);
    }

    [Fact]
    public void PurgeExpiredEphemeralEvidence_purges_expired_frames_and_keeps_canonical_images()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.Observed("device", "blob/canonical.jpg", Now));   // kept
        var frame = asset.Attach(AssetEvidence.EphemeralFrame("device", "blob/frame.jpg", Now, retentionUntil: Now.AddDays(7)));

        var deleted = asset.PurgeExpiredEphemeralEvidence(Now.AddDays(8));

        Assert.Equal(["blob/frame.jpg"], deleted);
        Assert.NotNull(frame.PurgedAt);
        Assert.Null(frame.Reference);   // the blob key is forgotten once deleted
        var canonical = asset.Evidence.Single(e => !e.IsEphemeral);
        Assert.Equal("blob/canonical.jpg", canonical.Reference);   // canonical evidence untouched
    }

    [Fact]
    public void PurgeExpiredEphemeralEvidence_leaves_a_frame_inside_its_window()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.EphemeralFrame("device", "blob/frame.jpg", Now, retentionUntil: Now.AddDays(7)));

        var deleted = asset.PurgeExpiredEphemeralEvidence(Now.AddDays(3));

        Assert.Empty(deleted);
        Assert.Single(asset.Evidence, e => e.Reference == "blob/frame.jpg" && e.PurgedAt is null);
    }

    [Fact]
    public void PurgeExpiredEphemeralEvidence_is_idempotent()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.EphemeralFrame("device", "blob/frame.jpg", Now, retentionUntil: Now.AddDays(1)));

        asset.PurgeExpiredEphemeralEvidence(Now.AddDays(2));
        var second = asset.PurgeExpiredEphemeralEvidence(Now.AddDays(2));

        Assert.Empty(second);
    }

    [Fact]
    public void BestReplacementValue_is_null_with_no_value_evidence()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Sofa", AssetCategory.Furniture, Now);

        Assert.Null(asset.BestReplacementValue);
    }

    [Fact]
    public void BestReplacementValue_prefers_independent_over_a_self_asserted_estimate()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Rolex", AssetCategory.Jewellery, Now);
        asset.Attach(Kadari.Domain.Assets.AssetEvidence.ValueEstimated("gemini", Money.Zar(50_000_00), Now.AddDays(2)));
        asset.Attach(Kadari.Domain.Assets.AssetEvidence.ValueSupported("valuer", Money.Zar(80_000_00), "blob://cert.pdf", Now));

        Assert.Equal(80_000_00, asset.BestReplacementValue!.AmountMinor);
    }

    [Fact]
    public void BestReplacementValue_takes_the_most_recent_within_a_strength()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        asset.Attach(Kadari.Domain.Assets.AssetEvidence.ValueEstimated("gemini", Money.Zar(10_000_00), Now));
        asset.Attach(Kadari.Domain.Assets.AssetEvidence.ValueEstimated("gemini", Money.Zar(12_000_00), Now.AddDays(1)));

        Assert.Equal(12_000_00, asset.BestReplacementValue!.AmountMinor);
    }

    [Fact]
    public void EraseEvidenceFiles_forgets_canonical_files_too_and_returns_them_once()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "MacBook Air", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.Observed("device", "kadari/c/snap.img", Now));
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", "kadari/c/receipts/r.doc", Now));
        asset.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(1_000_000), Now));

        var first = asset.EraseEvidenceFiles(Now);
        var second = asset.EraseEvidenceFiles(Now);

        Assert.Equal(["kadari/c/snap.img", "kadari/c/receipts/r.doc"], first);
        Assert.Empty(second);
        Assert.All(asset.Evidence, e => Assert.Null(e.Reference));
    }

    [Fact]
    public void HandOverToCoOwner_makes_the_longest_standing_co_owner_the_owner()
    {
        var erased = OwnerId.New();
        var partner = OwnerId.New();
        var child = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, erased, "Couch", AssetCategory.Furniture, Now);
        asset.AddCoOwner(partner, Now);
        asset.AddCoOwner(child, Now.AddDays(1));

        var successor = asset.HandOverToCoOwner(Now.AddDays(2));

        Assert.Equal(partner, successor);
        Assert.Equal(partner, asset.OwnerId);
        Assert.Equal([child], asset.CoOwners.Select(c => c.MemberId));
    }

    [Fact]
    public void HandOverToCoOwner_without_a_household_returns_null_and_keeps_the_owner()
    {
        var owner = OwnerId.New();
        var asset = Asset.Register(TenantId.Default, owner, "Couch", AssetCategory.Furniture, Now);

        var successor = asset.HandOverToCoOwner(Now);

        Assert.Null(successor);
        Assert.Equal(owner, asset.OwnerId);
    }
}
