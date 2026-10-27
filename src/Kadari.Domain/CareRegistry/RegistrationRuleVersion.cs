using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

public readonly record struct RegistrationRuleVersionId(Guid Value);

/// <summary>How a registration is carried out (CARE/07 §6, CARE/06 §9.1).</summary>
public enum RegistrationMode
{
    /// <summary>KADARI submits on the owner's behalf. Not executed in V1 (no OEM integration yet): treated as Assisted.</summary>
    Automatic,

    /// <summary>KADARI prepares everything; the owner does the one step only they can (OTP, login, final submit).</summary>
    Assisted,

    /// <summary>KADARI walks the owner through the official process and keeps the proof.</summary>
    Guided,
}

/// <summary>Where a registration goes.</summary>
public enum RegistrationDestinationType
{
    WebPage,
    Email,
    Phone,
}

/// <summary>The facts a registration needs to share — and only these leave KADARI (CARE/16 §2.15).</summary>
public enum RegistrationField
{
    OwnerName,
    Email,
    Mobile,
    Brand,
    Model,
    SerialNumber,
    PurchaseDate,
    Retailer,
    ProofOfPurchase,
}

/// <summary>
/// One immutable version of a registration rule (CARE/12 §4 RegistrationRuleVersion): "registering an eligible Bosch
/// appliance on MyBosch within 28 days of purchase adds 24 months, per this source". Like a coverage rule it never
/// changes after creation — a change is the next version, publishing supersedes the live one — so a registration keeps
/// pointing at exactly the rule it followed.
/// </summary>
public sealed class RegistrationRuleVersion : AggregateRoot<RegistrationRuleVersionId>
{
    public const int MaxTextLength = 500;

    private RegistrationRuleVersion() { }   // EF

    private RegistrationRuleVersion(RegistrationRuleVersionId id) : base(id) { }

    public Guid LogicalRuleId { get; private init; }
    public int Version { get; private init; }
    public string CountryCode { get; private init; } = null!;
    public BrandId BrandId { get; private init; }
    public CareCategory? Category { get; private init; }
    public string? ModelCode { get; private init; }
    public RegistrationMode Mode { get; private init; }
    public RegistrationDestinationType DestinationType { get; private init; }
    public string Destination { get; private init; } = null!;
    public RegistrationField[] RequiredFields { get; private init; } = [];
    public int? DeadlineDays { get; private init; }
    public int? ExtensionMonths { get; private init; }
    public string BenefitDescription { get; private init; } = null!;
    public string? Instructions { get; private init; }
    public RegistryConfidence Confidence { get; private init; }
    public SourceEvidenceId SourceEvidenceId { get; private init; }
    public DateTimeOffset LastVerifiedAt { get; private init; }
    public RuleStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? StatusChangedAt { get; private set; }

    public bool IsSelectable => Status is RuleStatus.Published or RuleStatus.Stale;

    public bool IsStaleAt(DateTimeOffset now) => Status == RuleStatus.Stale || now - LastVerifiedAt > CoverageRuleVersion.FreshnessWindow;

    /// <summary>Whether this rule applies to the product — brand always, category and model where the rule names them.</summary>
    public bool AppliesTo(string countryCode, BrandId? brand, CareCategory? category, string? modelCode) =>
        CountryCode == countryCode && BrandId == brand
        && (Category is null || Category == category)
        && (ModelCode is null || ModelCode == CoverageRuleVersion.NormaliseModel(modelCode));

    public bool HasSameContentAs(RegistrationRuleVersion other) =>
        CountryCode == other.CountryCode && BrandId == other.BrandId && Category == other.Category && ModelCode == other.ModelCode
        && Mode == other.Mode && DestinationType == other.DestinationType && Destination == other.Destination
        && RequiredFields.SequenceEqual(other.RequiredFields) && DeadlineDays == other.DeadlineDays
        && ExtensionMonths == other.ExtensionMonths && BenefitDescription == other.BenefitDescription
        && Instructions == other.Instructions && Confidence == other.Confidence
        && SourceEvidenceId == other.SourceEvidenceId && LastVerifiedAt == other.LastVerifiedAt;

    public static RegistrationRuleVersion Draft(
        RegistrationRuleVersionId id, Guid logicalRuleId, int version, string countryCode, BrandId brandId,
        CareCategory? category, string? modelCode, RegistrationMode mode, RegistrationDestinationType destinationType,
        string destination, IEnumerable<RegistrationField> requiredFields, int? deadlineDays, int? extensionMonths,
        string benefitDescription, string? instructions, RegistryConfidence confidence, SourceEvidenceId sourceEvidenceId,
        DateTimeOffset lastVerifiedAt)
    {
        if (logicalRuleId == Guid.Empty || version < 1)
        {
            throw new DomainException("A rule needs a logical id and a version from 1.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(benefitDescription);
        var cleanDestination = destination.Trim();
        if (destinationType == RegistrationDestinationType.WebPage
            && (!Uri.TryCreate(cleanDestination, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("A registration web page must be an https URL.");
        }

        if (deadlineDays is <= 0 or > 3650 || extensionMonths is <= 0 or > 600)
        {
            throw new DomainException("Deadlines and extensions must be positive and plausible.");
        }

        var fields = requiredFields.Distinct().Order().ToArray();
        var benefit = benefitDescription.Trim();
        if (benefit.Length > MaxTextLength || instructions?.Trim().Length > MaxTextLength * 2)
        {
            throw new DomainException("A registration rule's text is too long.");
        }

        return new RegistrationRuleVersion(id)
        {
            LogicalRuleId = logicalRuleId,
            Version = version,
            CountryCode = RegistryText.Country(countryCode),
            BrandId = brandId,
            Category = category,
            ModelCode = CoverageRuleVersion.NormaliseModel(modelCode),
            Mode = mode,
            DestinationType = destinationType,
            Destination = cleanDestination,
            RequiredFields = fields,
            DeadlineDays = deadlineDays,
            ExtensionMonths = extensionMonths,
            BenefitDescription = benefit,
            Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim(),
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

    public void MarkStale(DateTimeOffset at) => MoveTo(RuleStatus.Stale, at, RuleStatus.Published);

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
