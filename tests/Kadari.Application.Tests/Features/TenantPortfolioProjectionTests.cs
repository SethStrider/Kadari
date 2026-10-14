using Kadari.Application.Features.GetTenantPortfolio;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Tests.Features;

public class TenantPortfolioProjectionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly Guid Tenant = Guid.CreateVersion7();

    private static Asset Observed(OwnerId owner, string name, AssetCategory category)
    {
        var asset = Asset.Register(TenantId.Default, owner, name, category, Now);
        asset.Attach(AssetEvidence.Observed("device", $"blob/{name}.jpg", Now));
        return asset;
    }

    private static Asset Identified(OwnerId owner, string name, AssetCategory category, Money? value)
    {
        var asset = Observed(owner, name, category);
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        if (value is not null)
        {
            asset.Attach(AssetEvidence.ValueEstimated("ai-baseline", value, Now));
        }

        return asset;
    }

    private static Asset ValueSupported(OwnerId owner, string name, AssetCategory category, Money value)
    {
        var asset = Identified(owner, name, category, value: null);
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", $"blob/{name}-receipt.pdf", Now));
        asset.Attach(AssetEvidence.ValueSupported("valuer", value, $"cert/{name}", Now));
        return asset;
    }

    [Fact]
    public void Empty_cohort_projects_to_zeros()
    {
        var portfolio = TenantPortfolioProjection.From(Tenant, []);

        Assert.Equal(0, portfolio.AssetCount);
        Assert.Equal(0, portfolio.PolicyholderCount);
        Assert.Equal(0, portfolio.RecordedReplacementExposureMinor);
        Assert.Equal(0, portfolio.CompletenessPercent);
        Assert.Empty(portfolio.Categories);
    }

    [Fact]
    public void Portfolio_aggregates_counts_exposure_and_policyholders()
    {
        var alice = OwnerId.New();
        var bob = OwnerId.New();
        var assets = new[]
        {
            Identified(alice, "TV", AssetCategory.Electronics, Money.Zar(2_749_900)),
            Observed(alice, "Sofa", AssetCategory.Furniture),                        // no value
            ValueSupported(bob, "Ring", AssetCategory.Jewellery, Money.Zar(8_000_000)),
        };

        var portfolio = TenantPortfolioProjection.From(Tenant, assets);

        Assert.Equal(3, portfolio.AssetCount);
        Assert.Equal(2, portfolio.PolicyholderCount);                       // alice + bob
        Assert.Equal(2_749_900 + 8_000_000, portfolio.RecordedReplacementExposureMinor);
        Assert.Equal("ZAR", portfolio.Currency);
        Assert.Equal(67, portfolio.CompletenessPercent);                    // 2 of 3 valued
    }

    [Fact]
    public void Portfolio_bands_assets_by_assurance_level()
    {
        var owner = OwnerId.New();
        var assets = new[]
        {
            Observed(owner, "Sofa", AssetCategory.Furniture),                        // Observed
            Identified(owner, "TV", AssetCategory.Electronics, Money.Zar(2_749_900)), // Identified
            ValueSupported(owner, "Ring", AssetCategory.Jewellery, Money.Zar(8_000_000)), // ValueSupported
        };

        var portfolio = TenantPortfolioProjection.From(Tenant, assets);

        int Band(string level) => portfolio.AssuranceBreakdown.Single(b => b.Assurance == level).Count;
        Assert.Equal(1, Band(nameof(AssetAssuranceLevel.Observed)));
        Assert.Equal(1, Band(nameof(AssetAssuranceLevel.Identified)));
        Assert.Equal(1, Band(nameof(AssetAssuranceLevel.ValueSupported)));
        Assert.Equal(0, Band(nameof(AssetAssuranceLevel.None)));
    }

    [Fact]
    public void Categories_are_ordered_by_exposure_descending()
    {
        var owner = OwnerId.New();
        var assets = new[]
        {
            Identified(owner, "TV", AssetCategory.Electronics, Money.Zar(2_749_900)),
            ValueSupported(owner, "Ring", AssetCategory.Jewellery, Money.Zar(8_000_000)),
        };

        var portfolio = TenantPortfolioProjection.From(Tenant, assets);

        Assert.Equal("Jewellery", portfolio.Categories[0].Category);        // highest exposure first
        Assert.Equal(8_000_000, portfolio.Categories[0].ExposureMinor);
        Assert.Equal("Electronics", portfolio.Categories[1].Category);
    }

    /// <summary>
    /// GL-37: the streaming accumulator keeps the list projection's currency rule — the first valued asset's currency
    /// wins, and values in any other currency add to no exposure figure, however late they arrive.
    /// </summary>
    [Fact]
    public void Exposure_counts_only_the_first_valued_currency_even_when_others_come_first_in_a_category()
    {
        var owner = OwnerId.New();
        var assets = new[]
        {
            Observed(owner, "Sofa", AssetCategory.Furniture),                                          // unvalued first
            Identified(owner, "Watch", AssetCategory.Jewellery, new Money(50_000, "USD")),             // first valued: USD
            Identified(owner, "TV", AssetCategory.Electronics, Money.Zar(2_749_900)),                   // other currency
            Identified(owner, "Ring", AssetCategory.Jewellery, new Money(120_000, "USD")),
        };

        var portfolio = TenantPortfolioProjection.From(Tenant, assets);

        Assert.Equal("USD", portfolio.Currency);
        Assert.Equal(170_000, portfolio.RecordedReplacementExposureMinor);
        Assert.Equal(
            [("Jewellery", 2, 170_000L), ("Electronics", 1, 0L), ("Furniture", 1, 0L)],
            portfolio.Categories.Select(c => (c.Category, c.Count, c.ExposureMinor)));
        Assert.Equal(75, portfolio.CompletenessPercent);   // 3 of 4 valued, whatever the currency
    }
}
