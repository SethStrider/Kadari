using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

public readonly record struct RoutingRuleVersionId(Guid Value);

/// <summary>Where a problem goes (CARE/12 §10 RouteType).</summary>
public enum RouteType
{
    RetailerReturn,
    RetailerCredit,
    OemDirect,
    WarrantyAdmin,
    AuthorisedService,
    Onsite,
    CarryIn,
    Collection,
    ProtectionProvider,
    ResearchRequired,
}

/// <summary>Whether a rule applies to products in cover, out of it, or either.</summary>
public enum RouteCoverage
{
    Any,
    InCover,
    OutOfCover,
}

/// <summary>What kind of problem a rule is for.</summary>
public enum IssueClass
{
    Any,
    Fault,
    AccidentalDamage,
    DeadOnArrival,
}

/// <summary>How the owner reaches the route's counterparty.</summary>
public enum ContactChannelType
{
    WebPage,
    Email,
    Phone,
    WhatsApp,
    InStore,
}

/// <summary>Evidence a route asks for.</summary>
public enum RouteEvidence
{
    ProofOfPurchase,
    SerialNumber,
    FaultPhoto,
    FaultVideo,
    ErrorCode,
    InstallationCertificate,
}

/// <summary>
/// One immutable version of a routing rule (CARE/12 §4 RoutingRuleVersion): "a Hisense small appliance in cover with a
/// fault goes back to the retailer for credit, with the receipt" — the counterparty, how to reach them, what to take,
/// in what order of preference. Never changed after creation; a change is the next version.
/// </summary>
public sealed class RoutingRuleVersion : AggregateRoot<RoutingRuleVersionId>
{
    public const int MaxTextLength = 500;

    private RoutingRuleVersion() { }   // EF

    private RoutingRuleVersion(RoutingRuleVersionId id) : base(id) { }

    public Guid LogicalRuleId { get; private init; }
    public int Version { get; private init; }
    public string CountryCode { get; private init; } = null!;
    public BrandId? BrandId { get; private init; }
    public CareCategory? Category { get; private init; }
    public string? ModelCode { get; private init; }
    public RetailerId? RetailerId { get; private init; }
    public RouteCoverage Coverage { get; private init; }
    public IssueClass IssueClass { get; private init; }
    public RouteType RouteType { get; private init; }
    public string? RoutingModelCode { get; private init; }
    public string TargetName { get; private init; } = null!;
    public ContactChannelType ChannelType { get; private init; }
    public string? ChannelValue { get; private init; }
    public RouteEvidence[] RequiredEvidence { get; private init; } = [];
    public string Instructions { get; private init; } = null!;
    public int Priority { get; private init; }
    public RegistryConfidence Confidence { get; private init; }
    public SourceEvidenceId SourceEvidenceId { get; private init; }
    public DateTimeOffset LastVerifiedAt { get; private init; }
    public RuleStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? StatusChangedAt { get; private set; }

    public bool IsSelectable => Status is RuleStatus.Published or RuleStatus.Stale;

    public bool IsStaleAt(DateTimeOffset now) => Status == RuleStatus.Stale || now - LastVerifiedAt > CoverageRuleVersion.FreshnessWindow;

    public RuleSpecificity Specificity => ModelCode is not null
        ? RuleSpecificity.Model
        : BrandId is not null
            ? Category is not null ? RuleSpecificity.BrandCategory : RuleSpecificity.Brand
            : RuleSpecificity.RetailerCategory;

    public bool HasSameContentAs(RoutingRuleVersion o) =>
        CountryCode == o.CountryCode && BrandId == o.BrandId && Category == o.Category && ModelCode == o.ModelCode
        && RetailerId == o.RetailerId && Coverage == o.Coverage && IssueClass == o.IssueClass && RouteType == o.RouteType
        && RoutingModelCode == o.RoutingModelCode && TargetName == o.TargetName && ChannelType == o.ChannelType
        && ChannelValue == o.ChannelValue && RequiredEvidence.SequenceEqual(o.RequiredEvidence)
        && Instructions == o.Instructions && Priority == o.Priority && Confidence == o.Confidence
        && SourceEvidenceId == o.SourceEvidenceId && LastVerifiedAt == o.LastVerifiedAt;

    public static RoutingRuleVersion Draft(
        RoutingRuleVersionId id, Guid logicalRuleId, int version, string countryCode, BrandId? brandId,
        CareCategory? category, string? modelCode, RetailerId? retailerId, RouteCoverage coverage, IssueClass issueClass,
        RouteType routeType, string? routingModelCode, string targetName, ContactChannelType channelType,
        string? channelValue, IEnumerable<RouteEvidence> requiredEvidence, string instructions, int priority,
        RegistryConfidence confidence, SourceEvidenceId sourceEvidenceId, DateTimeOffset lastVerifiedAt)
    {
        if (logicalRuleId == Guid.Empty || version < 1)
        {
            throw new DomainException("A rule needs a logical id and a version from 1.");
        }

        if (brandId is null && (retailerId is null || category is null))
        {
            throw new DomainException("A routing rule names a brand, or a retailer and a category.");
        }

        if (routeType == RouteType.ResearchRequired)
        {
            throw new DomainException("Research is what happens when no rule applies; it is not a rule.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        if (channelType == ContactChannelType.WebPage
            && (!Uri.TryCreate(channelValue, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("A web-page contact must be an https URL.");
        }

        if (channelType != ContactChannelType.InStore && string.IsNullOrWhiteSpace(channelValue))
        {
            throw new DomainException("Say how to reach the counterparty.");
        }

        if (instructions.Trim().Length > MaxTextLength * 2 || targetName.Trim().Length > 200 || priority is < 0 or > 1000)
        {
            throw new DomainException("A routing rule's text or priority is out of range.");
        }

        return new RoutingRuleVersion(id)
        {
            LogicalRuleId = logicalRuleId,
            Version = version,
            CountryCode = RegistryText.Country(countryCode),
            BrandId = brandId,
            Category = category,
            ModelCode = CoverageRuleVersion.NormaliseModel(modelCode),
            RetailerId = retailerId,
            Coverage = coverage,
            IssueClass = issueClass,
            RouteType = routeType,
            RoutingModelCode = string.IsNullOrWhiteSpace(routingModelCode) ? null : routingModelCode.Trim().ToUpperInvariant(),
            TargetName = targetName.Trim(),
            ChannelType = channelType,
            ChannelValue = string.IsNullOrWhiteSpace(channelValue) ? null : channelValue.Trim(),
            RequiredEvidence = requiredEvidence.Distinct().Order().ToArray(),
            Instructions = instructions.Trim(),
            Priority = priority,
            Confidence = confidence,
            SourceEvidenceId = sourceEvidenceId,
            LastVerifiedAt = lastVerifiedAt,
            Status = RuleStatus.Draft,
        };
    }

    public void Publish(DateTimeOffset at)
    {
        if (Status != RuleStatus.Draft)
        {
            throw new DomainException($"Only a draft can be published; this version is {Status}.");
        }

        Status = RuleStatus.Published;
        PublishedAt = at;
        StatusChangedAt = at;
    }

    public void Supersede(DateTimeOffset at) => MoveTo(RuleStatus.Superseded, at, RuleStatus.Published, RuleStatus.Stale);

    public void Retire(DateTimeOffset at) =>
        MoveTo(RuleStatus.Retired, at, RuleStatus.Draft, RuleStatus.Published, RuleStatus.Stale, RuleStatus.Superseded);

    private void MoveTo(RuleStatus target, DateTimeOffset at, params RuleStatus[] from)
    {
        if (!from.Contains(Status))
        {
            throw new DomainException($"A {Status} rule version cannot become {target}.");
        }

        Status = target;
        StatusChangedAt = at;
    }
}
