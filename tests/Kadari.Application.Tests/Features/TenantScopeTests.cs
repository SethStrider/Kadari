using Kadari.Application.Abstractions.Access;
using Kadari.Application.Access;
using Kadari.Application.Features.Tenancy;
using Kadari.Domain.Assets;

namespace Kadari.Application.Tests.Features;

public class TenantScopeTests
{
    [Fact]
    public void ForCurrentTenant_keeps_only_rows_in_the_current_tenant()
    {
        var mine = TenantId.Default;
        var other = TenantId.New();
        var rows = new[] { new ScopedRow(mine), new ScopedRow(other), new ScopedRow(mine) }.AsQueryable();

        var filtered = rows.ForCurrentTenant(new FixedTenant(mine)).ToList();

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, r => Assert.Equal(mine, r.TenantId));
    }

    [Fact]
    public void ForCurrentTenant_filters_out_a_foreign_tenants_rows()
    {
        var other = TenantId.New();
        var rows = new[] { new ScopedRow(other) }.AsQueryable();

        var filtered = rows.ForCurrentTenant(new FixedTenant(TenantId.Default)).ToList();

        Assert.Empty(filtered);
    }

    [Fact]
    public void DefaultTenant_resolves_every_caller_to_the_platform_tenant()
    {
        Assert.Equal(TenantId.Default, new DefaultTenant().Current);
    }

    private sealed record ScopedRow(TenantId TenantId) : ITenantScoped;

    private sealed class FixedTenant(TenantId current) : ICurrentTenant
    {
        public TenantId Current { get; } = current;
    }
}
