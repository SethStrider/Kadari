using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Tests.CareRegistry;

/// <summary>Builds coverage rule versions for tests. Defaults: a published, high-confidence, current 24-month
/// manufacturer warranty for <see cref="Bosch"/>, from purchase, in ZA.</summary>
internal static class RuleFactory
{
    public static readonly BrandId Bosch = new(Guid.Parse("0192f000-0000-7000-8000-000000000001"));
    public static readonly BrandId Hisense = new(Guid.Parse("0192f000-0000-7000-8000-000000000002"));
    public static readonly RetailerId Makro = new(Guid.Parse("0192f000-0000-7000-8000-0000000000a1"));
    public static readonly SourceEvidenceId Source = new(Guid.Parse("0192f000-0000-7000-8000-0000000000f1"));
    public static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    public static CoverageRuleVersion Rule(
        BrandId? brand = null,
        CareCategory? category = null,
        string? model = null,
        RetailerId? retailer = null,
        int? months = 24,
        CoverageType type = CoverageType.Manufacturer,
        RegistryConfidence confidence = RegistryConfidence.High,
        bool registrationRequired = false,
        CoverageStartBasis startBasis = CoverageStartBasis.PurchaseDate,
        DateTimeOffset? lastVerified = null,
        Guid? logicalId = null,
        int version = 1,
        bool publish = true,
        bool noBrand = false)
    {
        var rule = CoverageRuleVersion.Draft(
            new CoverageRuleVersionId(Guid.CreateVersion7()), logicalId ?? Guid.CreateVersion7(), version, "ZA",
            noBrand ? null : brand ?? Bosch, category, model, retailer, type, months, startBasis,
            registrationRequired, null, null, "Two-year manufacturer warranty from the date of purchase.",
            null, null, confidence, Source, lastVerified ?? Now.AddDays(-30));

        if (publish)
        {
            rule.Publish(Now.AddDays(-1));
        }

        return rule;
    }
}
