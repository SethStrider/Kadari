using Kadari.Domain.Assets;
using Kadari.Domain.Identity;

namespace Kadari.Domain.Tests.Assets;

public class InsurerUserTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly UserAccountId Account = UserAccountId.New();

    [Fact]
    public void Grant_binds_the_account_to_the_tenant()
    {
        var tenant = TenantId.New();

        var insurerUser = InsurerUser.Grant(Account, tenant, Now);

        Assert.Equal(Account, insurerUser.UserAccountId);
        Assert.Equal(tenant, insurerUser.TenantId);
        Assert.Equal(Now, insurerUser.GrantedAt);
    }

    [Fact]
    public void Reassign_moves_the_binding_to_a_new_tenant()
    {
        var insurerUser = InsurerUser.Grant(Account, TenantId.New(), Now);
        var next = TenantId.New();

        insurerUser.Reassign(next, Now.AddDays(1));

        Assert.Equal(next, insurerUser.TenantId);
        Assert.Equal(Now.AddDays(1), insurerUser.GrantedAt);
    }
}
