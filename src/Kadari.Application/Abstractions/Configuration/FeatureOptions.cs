namespace Kadari.Application.Abstractions.Configuration;

/// <summary>
/// Launch switches for capabilities that are safer turned off than hardened for the first release. Bound from the
/// <c>Features</c> section. Defaults keep everything on, so development and tests behave as before; production turns
/// them off in its environment.
/// </summary>
public sealed class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>
    /// Ownership changes: transfer, household members, estate settlement and the asset-for-sale export (with its
    /// public verification). No client surface uses them yet.
    /// </summary>
    public bool AssetOwnershipChanges { get; set; } = true;

    /// <summary>
    /// The insurer console: creating insurer tenants, assigning customers to them, granting insurer access, branding,
    /// and the insurer's portfolio and asset reads (AS-05). Off at launch: assigning a customer moves their whole
    /// ledger into an insurer's view, which needs the customer's own consent first.
    /// </summary>
    public bool InsurerConsole { get; set; } = true;

    /// <summary>
    /// KADARI Care on the customer's assets: the Care card, coverage, purchase details and asset history (CARE/14 C1).
    /// Off in production until the registry holds published, researched coverage rules — without them every asset
    /// would honestly read "not confirmed yet".
    /// </summary>
    public bool Care { get; set; } = true;
}
