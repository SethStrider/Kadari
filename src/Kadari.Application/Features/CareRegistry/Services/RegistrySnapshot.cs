namespace Kadari.Application.Features.CareRegistry.Services;

/// <summary>
/// The Care registry as read from its research files (<c>docs/authority/CARE/REGISTRY/*.csv</c> plus KADARI's own
/// researched rules), before anything touches the database. The research workbook is input, not a runtime datastore
/// (CARE/14 C2): an import turns it into versioned rows through a preview-then-apply plan.
/// </summary>
/// <param name="ResearchedAt">When the research behind the files was done — stamped as the sources' retrieval date,
/// since the workbook does not say per row.</param>
public sealed record RegistrySnapshot(
    DateTimeOffset ResearchedAt,
    string CountryCode,
    IReadOnlyList<BrandRow> Brands,
    IReadOnlyList<RetailerRow> Retailers,
    IReadOnlyList<RoutingModelRow> RoutingModels,
    IReadOnlyList<CoverageRuleRow> CoverageRules,
    IReadOnlyList<RegistrationRuleRow>? RegistrationRules = null,
    IReadOnlyList<RoutingRuleRow>? RoutingRules = null);

public sealed record BrandRow(
    int Line,
    string Name,
    string? ParentGroup,
    string? LocalOperator,
    string? Categories,
    string? ServiceModel,
    string? RegistrationSignal,
    string? Priority,
    string? ResearchConfidence,
    string? SourceUrl);

public sealed record RetailerRow(
    int Line,
    string Name,
    string? GroupOwner,
    string? RetailFormat,
    string? Categories,
    string? IntegrationStance,
    string? Priority,
    string? SourceUrl);

public sealed record RoutingModelRow(
    int Line,
    string Code,
    string Name,
    string? WhenItApplies,
    string? DefaultAction,
    string? TypicalEvidence,
    string? Examples);

/// <summary>One researched coverage rule. <see cref="RuleKey"/> is the stable logical name ("bosch-major-appliance");
/// a changed row becomes a new version of the same rule.</summary>
public sealed record CoverageRuleRow(
    int Line,
    string RuleKey,
    string? Country,
    string? Brand,
    string? Category,
    string? Model,
    string? Retailer,
    string? CoverageType,
    string? DurationMonths,
    string? StartBasis,
    string? RegistrationRequired,
    string? RegistrationDeadlineDays,
    string? ExtensionMonths,
    string? Summary,
    string? EffectiveFrom,
    string? EffectiveTo,
    string? Confidence,
    string? SourceTitle,
    string? SourceUrl,
    string? LastVerified,
    string? Status);

/// <summary>One researched registration rule. <see cref="RuleKey"/> is the stable logical name ("bosch-mybosch");
/// a changed row becomes a new version of the same rule.</summary>
public sealed record RegistrationRuleRow(
    int Line,
    string RuleKey,
    string? Country,
    string? Brand,
    string? Category,
    string? Model,
    string? Mode,
    string? DestinationType,
    string? Destination,
    string? RequiredFields,
    string? DeadlineDays,
    string? ExtensionMonths,
    string? Benefit,
    string? Instructions,
    string? Confidence,
    string? SourceTitle,
    string? SourceUrl,
    string? LastVerified,
    string? Status);

/// <summary>One researched routing rule — where a kind of problem goes. <see cref="RuleKey"/> is the stable logical
/// name ("hisense-small-appliance-fault"); a changed row becomes a new version of the same rule.</summary>
public sealed record RoutingRuleRow(
    int Line,
    string RuleKey,
    string? Country,
    string? Brand,
    string? Category,
    string? Model,
    string? Retailer,
    string? Coverage,
    string? IssueClass,
    string? RouteType,
    string? RoutingModel,
    string? Target,
    string? ChannelType,
    string? Channel,
    string? RequiredEvidence,
    string? Instructions,
    string? Priority,
    string? Confidence,
    string? SourceTitle,
    string? SourceUrl,
    string? LastVerified,
    string? Status);
