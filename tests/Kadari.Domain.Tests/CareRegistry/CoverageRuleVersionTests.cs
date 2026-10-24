using Kadari.Domain.Abstractions;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Tests.CareRegistry;

public class CoverageRuleVersionTests
{
    [Fact]
    public void Publish_WithoutADuration_Throws()
    {
        var rule = RuleFactory.Rule(months: null, publish: false);

        Assert.Throws<DomainException>(() => rule.Publish(RuleFactory.Now));
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_Throws()
    {
        var rule = RuleFactory.Rule();

        Assert.Throws<DomainException>(() => rule.Publish(RuleFactory.Now));
    }

    [Fact]
    public void Draft_WithNeitherBrandNorRetailerAndCategory_Throws()
        => Assert.Throws<DomainException>(() => RuleFactory.Rule(noBrand: true, retailer: RuleFactory.Makro, publish: false));

    [Fact]
    public void Draft_WithModelButNoBrand_Throws()
        => Assert.Throws<DomainException>(() =>
            RuleFactory.Rule(noBrand: true, model: "WGG244", retailer: RuleFactory.Makro, category: CareCategory.MajorAppliance, publish: false));

    [Fact]
    public void Supersede_KeepsTheVersionAddressableButNotSelectable()
    {
        var rule = RuleFactory.Rule();

        rule.Supersede(RuleFactory.Now);

        Assert.Equal((RuleStatus.Superseded, false), (rule.Status, rule.IsSelectable));
    }

    [Fact]
    public void Supersede_ADraft_Throws()
    {
        var rule = RuleFactory.Rule(publish: false);

        Assert.Throws<DomainException>(() => rule.Supersede(RuleFactory.Now));
    }

    [Fact]
    public void IsStaleAt_WhenNotReverifiedWithinAYear_IsTrue()
    {
        var rule = RuleFactory.Rule(lastVerified: RuleFactory.Now.AddDays(-400));

        Assert.True(rule.IsStaleAt(RuleFactory.Now));
    }

    [Fact]
    public void Specificity_OrdersModelAboveBrandCategoryAboveBrandAboveRetailerCategory()
    {
        var model = RuleFactory.Rule(model: "wgg 244").Specificity;
        var brandCategory = RuleFactory.Rule(category: CareCategory.MajorAppliance).Specificity;
        var brand = RuleFactory.Rule().Specificity;
        var retailer = RuleFactory.Rule(noBrand: true, retailer: RuleFactory.Makro, category: CareCategory.SmallAppliance).Specificity;

        Assert.True(model > brandCategory && brandCategory > brand && brand > retailer);
    }

    [Fact]
    public void HasSameContentAs_IgnoresIdentityAndVersion()
    {
        var logical = Guid.CreateVersion7();
        var v1 = RuleFactory.Rule(logicalId: logical, version: 1);
        var v2 = RuleFactory.Rule(logicalId: logical, version: 2, publish: false);

        Assert.True(v2.HasSameContentAs(v1));
    }

    [Fact]
    public void Slug_FoldsAccentsAndPunctuation()
        => Assert.Equal("delonghi", RegistrySlug.For("De'Longhi"));
}
