using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Tests.Assets;

public class TenantMembershipTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly CustomerId Customer = new(Guid.CreateVersion7());

    [Fact]
    public void Assign_records_the_customer_and_tenant()
    {
        var tenant = TenantId.New();

        var membership = TenantMembership.Assign(Customer, tenant, Now);

        Assert.Equal(Customer, membership.CustomerId);
        Assert.Equal(tenant, membership.TenantId);
        Assert.Equal(Now, membership.AssignedAt);
    }

    [Fact]
    public void Reassign_moves_the_customer_to_a_new_tenant()
    {
        var membership = TenantMembership.Assign(Customer, TenantId.Default, Now);
        var insurer = TenantId.New();

        membership.Reassign(insurer, Now.AddDays(1));

        Assert.Equal(insurer, membership.TenantId);
        Assert.Equal(Now.AddDays(1), membership.AssignedAt);
    }

    [Fact]
    public void Asset_ReassignTenant_repoints_the_partition()
    {
        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "TV", AssetCategory.Electronics, Now);
        var insurer = TenantId.New();

        asset.ReassignTenant(insurer);

        Assert.Equal(insurer, asset.TenantId);
    }
}
