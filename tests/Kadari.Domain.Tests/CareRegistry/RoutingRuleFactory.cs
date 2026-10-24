using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Tests.CareRegistry;

/// <summary>Builds routing rule versions for tests. Defaults: a published, high-confidence, current Bosch rule that
/// sends any problem to the OEM's support page.</summary>
internal static class RoutingRuleFactory
{
    public static RoutingRuleVersion Rule(
        RouteType route = RouteType.OemDirect,
        BrandId? brand = null,
        CareCategory? category = null,
        string? model = null,
        RetailerId? retailer = null,
        RouteCoverage coverage = RouteCoverage.Any,
        IssueClass issue = IssueClass.Any,
        int priority = 100,
        RegistryConfidence confidence = RegistryConfidence.High,
        DateTimeOffset? lastVerified = null,
        bool publish = true,
        bool noBrand = false,
        ContactChannelType channelType = ContactChannelType.WebPage,
        string? channel = "https://www.bosch-home.co.za/service")
    {
        var rule = RoutingRuleVersion.Draft(
            new RoutingRuleVersionId(Guid.CreateVersion7()), Guid.CreateVersion7(), 1, "ZA",
            noBrand ? null : brand ?? RuleFactory.Bosch, category, model, retailer, coverage, issue, route, null,
            "Bosch Home Service", channelType, channel, [RouteEvidence.ProofOfPurchase, RouteEvidence.SerialNumber],
            "Book a repair on the Bosch service page.", priority, confidence, RuleFactory.Source,
            lastVerified ?? RuleFactory.Now.AddDays(-30));

        if (publish)
        {
            rule.Publish(RuleFactory.Now.AddDays(-1));
        }

        return rule;
    }
}
