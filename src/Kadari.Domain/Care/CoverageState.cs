namespace Kadari.Domain.Care;

/// <summary>
/// What KADARI can honestly say about an asset's cover (CARE/12 §10). <see cref="Active"/>, <see cref="Expiring"/>
/// and <see cref="Expired"/> are only claimed on independent purchase evidence and a current, confident rule;
/// anything weaker is <see cref="LikelyActive"/> or <see cref="Unknown"/> — never a guess.
/// </summary>
public enum CoverageState
{
    Unknown,
    LikelyActive,
    Active,
    Expiring,
    Expired,
    VoidOrIneligible,
    NeedsReview,
}
