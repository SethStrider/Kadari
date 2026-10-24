namespace Kadari.Domain.CareRegistry;

/// <summary>How far KADARI trusts a registry fact (CARE/12 §10). Research confidence, not a probability.</summary>
public enum RegistryConfidence
{
    Low,
    Medium,
    High,
    Verified,
}

/// <summary>
/// The life of a rule version (CARE/12 §10). Only <see cref="Published"/> versions are selected for new decisions;
/// <see cref="Stale"/> versions are still selectable but can never make a decision more than "likely";
/// <see cref="Superseded"/> and <see cref="Retired"/> stay addressable so a past decision can always be reconstructed.
/// </summary>
public enum RuleStatus
{
    Draft,
    Published,
    Stale,
    Superseded,
    Retired,
}

/// <summary>The kind of cover a coverage rule describes.</summary>
public enum CoverageType
{
    Manufacturer,
    RetailerGuarantee,
    Extended,
    ServicePlan,
}

/// <summary>What a warranty clock starts from.</summary>
public enum CoverageStartBasis
{
    PurchaseDate,
    DeliveryDate,
    RegistrationDate,
}

/// <summary>
/// The product category KADARI Care reasons about — finer than the insurance <c>AssetCategory</c> (a washing machine,
/// a kettle and a laptop are all "Electronics" there) and the axis coverage and routing rules are written against.
/// </summary>
public enum CareCategory
{
    MajorAppliance,
    SmallAppliance,
    TvAndAudio,
    Mobile,
    Computing,
    PowerTools,
    PortablePower,
    Other,
}
