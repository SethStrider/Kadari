using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

public readonly record struct CoverageRuleVersionId(Guid Value);

/// <summary>How specific a rule is — the order the coverage engine prefers them in (CARE/03 §2B).</summary>
public enum RuleSpecificity
{
    RetailerCategory = 1,
    Brand = 2,
    BrandCategory = 3,
    Model = 4,
}

/// <summary>
/// One immutable version of a coverage rule (CARE/12 §4): "a Bosch major appliance bought in ZA carries a 24-month
/// manufacturer warranty from purchase, per this source, verified on this date". Every field is fixed at creation —
/// a change is a new version of the same <see cref="LogicalRuleId"/>, and publishing it supersedes the old one — so a
/// decision that recorded this version's id can always be reconstructed exactly (CARE/15 G0). Only the lifecycle
/// (<see cref="Status"/>) moves, and only forward.
/// </summary>
public sealed class CoverageRuleVersion : AggregateRoot<CoverageRuleVersionId>
{
    public const int MaxSummaryLength = 500;
    public const int MaxModelLength = 80;

    /// <summary>A rule not re-verified within this window is due for review and can no longer make a decision more
    /// than "likely" (CARE/07 §17, REVIEW_DUE).</summary>
    public static readonly TimeSpan FreshnessWindow = TimeSpan.FromDays(365);

    private CoverageRuleVersion() { }   // EF

    private CoverageRuleVersion(CoverageRuleVersionId id) : base(id) { }

    public Guid LogicalRuleId { get; private init; }
    public int Version { get; private init; }
    public string CountryCode { get; private init; } = null!;

    public BrandId? BrandId { get; private init; }
    public CareCategory? Category { get; private init; }

    /// <summary>The model code this rule is specific to, normalised (upper-case, no spaces); null for broader rules.</summary>
    public string? ModelCode { get; private init; }

    public RetailerId? RetailerId { get; private init; }

    public CoverageType CoverageType { get; private init; }
    public int? DurationMonths { get; private init; }
    public CoverageStartBasis StartBasis { get; private init; }
    public bool RegistrationRequired { get; private init; }
    public int? RegistrationDeadlineDays { get; private init; }
    public int? ExtensionMonths { get; private init; }
    public string PlainLanguageSummary { get; private init; } = null!;
    public DateOnly? EffectiveFrom { get; private init; }
    public DateOnly? EffectiveTo { get; private init; }
    public RegistryConfidence Confidence { get; private init; }
    public SourceEvidenceId SourceEvidenceId { get; private init; }
    public DateTimeOffset LastVerifiedAt { get; private init; }

    public RuleStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? StatusChangedAt { get; private set; }
    public CoverageRuleVersionId? SupersedesVersionId { get; private init; }

    public RuleSpecificity Specificity => ModelCode is not null
        ? RuleSpecificity.Model
        : BrandId is not null
            ? Category is not null ? RuleSpecificity.BrandCategory : RuleSpecificity.Brand
            : RuleSpecificity.RetailerCategory;

    /// <summary>Whether this version may be chosen for a new decision.</summary>
    public bool IsSelectable => Status is RuleStatus.Published or RuleStatus.Stale;

    /// <summary>Whether the rule is stale — marked so, or not re-verified within <see cref="FreshnessWindow"/>.</summary>
    public bool IsStaleAt(DateTimeOffset now) => Status == RuleStatus.Stale || now - LastVerifiedAt > FreshnessWindow;

    /// <summary>Whether the rule was in force on <paramref name="date"/>.</summary>
    public bool IsEffectiveOn(DateOnly date) =>
        (EffectiveFrom is null || date >= EffectiveFrom) && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>The same rule, field for field — a re-import of identical content must not mint a new version.</summary>
    public bool HasSameContentAs(CoverageRuleVersion other) =>
        CountryCode == other.CountryCode && BrandId == other.BrandId && Category == other.Category
        && ModelCode == other.ModelCode && RetailerId == other.RetailerId && CoverageType == other.CoverageType
        && DurationMonths == other.DurationMonths && StartBasis == other.StartBasis
        && RegistrationRequired == other.RegistrationRequired && RegistrationDeadlineDays == other.RegistrationDeadlineDays
        && ExtensionMonths == other.ExtensionMonths && PlainLanguageSummary == other.PlainLanguageSummary
        && EffectiveFrom == other.EffectiveFrom && EffectiveTo == other.EffectiveTo && Confidence == other.Confidence
        && SourceEvidenceId == other.SourceEvidenceId && LastVerifiedAt == other.LastVerifiedAt;

    /// <summary>
    /// Draft a version. A rule must name a brand, or a retailer and a category (a retailer guarantee) — a rule about
    /// "everything" would be a guess. A model-specific rule must also name its brand.
    /// </summary>
    public static CoverageRuleVersion Draft(
        CoverageRuleVersionId id, Guid logicalRuleId, int version, string countryCode,
        BrandId? brandId, CareCategory? category, string? modelCode, RetailerId? retailerId,
        CoverageType coverageType, int? durationMonths, CoverageStartBasis startBasis,
        bool registrationRequired, int? registrationDeadlineDays, int? extensionMonths,
        string plainLanguageSummary, DateOnly? effectiveFrom, DateOnly? effectiveTo,
        RegistryConfidence confidence, SourceEvidenceId sourceEvidenceId, DateTimeOffset lastVerifiedAt,
        CoverageRuleVersionId? supersedesVersionId = null)
    {
        if (logicalRuleId == Guid.Empty)
        {
            throw new DomainException("A rule needs a logical id.");
        }

        if (version < 1)
        {
            throw new DomainException("Rule versions start at 1.");
        }

        var model = NormaliseModel(modelCode);
        if (brandId is null && (retailerId is null || category is null))
        {
            throw new DomainException("A coverage rule names a brand, or a retailer and a category.");
        }

        if (model is not null && brandId is null)
        {
            throw new DomainException("A model-specific rule must name its brand.");
        }

        if (durationMonths is <= 0 or > 600 || extensionMonths is <= 0 or > 600 || registrationDeadlineDays is <= 0 or > 3650)
        {
            throw new DomainException("Durations and deadlines must be positive and plausible.");
        }

        if (registrationDeadlineDays is not null && !registrationRequired)
        {
            throw new DomainException("A registration deadline only applies when registration is required.");
        }

        if (effectiveFrom is not null && effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new DomainException("A rule cannot stop being effective before it starts.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(plainLanguageSummary);
        var summary = plainLanguageSummary.Trim();
        if (summary.Length > MaxSummaryLength)
        {
            throw new DomainException($"A rule summary is at most {MaxSummaryLength} characters.");
        }

        return new CoverageRuleVersion(id)
        {
            LogicalRuleId = logicalRuleId,
            Version = version,
            CountryCode = RegistryText.Country(countryCode),
            BrandId = brandId,
            Category = category,
            ModelCode = model,
            RetailerId = retailerId,
            CoverageType = coverageType,
            DurationMonths = durationMonths,
            StartBasis = startBasis,
            RegistrationRequired = registrationRequired,
            RegistrationDeadlineDays = registrationDeadlineDays,
            ExtensionMonths = extensionMonths,
            PlainLanguageSummary = summary,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Confidence = confidence,
            SourceEvidenceId = sourceEvidenceId,
            LastVerifiedAt = lastVerifiedAt,
            Status = RuleStatus.Draft,
            SupersedesVersionId = supersedesVersionId,
        };
    }

    /// <summary>
    /// Make the version live. A coverage rule without a duration cannot be published: it would let the engine state
    /// a warranty period nobody verified. Draft → Published only.
    /// </summary>
    public void Publish(DateTimeOffset at)
    {
        if (Status != RuleStatus.Draft)
        {
            throw new DomainException($"Only a draft can be published; this version is {Status}.");
        }

        if (DurationMonths is null)
        {
            throw new DomainException("A coverage rule needs a verified duration before it can be published.");
        }

        Status = RuleStatus.Published;
        PublishedAt = at;
        StatusChangedAt = at;
        RaiseDomainEvent(new Events.RegistryRulePublishedDomainEvent(Id, LogicalRuleId, Version, at));
    }

    /// <summary>A newer version of the same rule went live. Published/Stale → Superseded.</summary>
    public void Supersede(DateTimeOffset at) => MoveTo(RuleStatus.Superseded, at, RuleStatus.Published, RuleStatus.Stale);

    /// <summary>The source needs re-checking. Published → Stale.</summary>
    public void MarkStale(DateTimeOffset at) => MoveTo(RuleStatus.Stale, at, RuleStatus.Published);

    /// <summary>The rule no longer applies to new decisions. Any status but Retired → Retired.</summary>
    public void Retire(DateTimeOffset at) =>
        MoveTo(RuleStatus.Retired, at, RuleStatus.Draft, RuleStatus.Published, RuleStatus.Stale, RuleStatus.Superseded);

    public static string? NormaliseModel(string? modelCode)
    {
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            return null;
        }

        var normalised = string.Concat(modelCode.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        return normalised.Length > MaxModelLength
            ? throw new DomainException($"A model code is at most {MaxModelLength} characters.")
            : normalised;
    }

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
