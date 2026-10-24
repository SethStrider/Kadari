namespace Kadari.Domain.Access;

/// <summary>
/// The role → permission grants, least-privilege. The single source of truth for "which role may do what".
/// Resource-scoped restrictions (ownership) are layered on top via <see cref="AccessPolicies"/>.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<Role, HashSet<Permission>> Map = new Dictionary<Role, HashSet<Permission>>
    {
        [Role.PublicVisitor] = [],

        // Self-service: capture into and view your own ledger, export an asset for sale, transfer one you own, and
        // use KADARI Care on your own assets.
        [Role.Customer] =
        [
            Permission.AssetCapture, Permission.AssetView, Permission.AssetExport, Permission.AssetTransfer,
            Permission.CareView, Permission.CareManage,
        ],

        // A professional (independent) valuation lifts an asset to Value supported, which the customer's own AI
        // baseline can never reach.
        [Role.Valuer] = [Permission.AssetView, Permission.AssetValuate],

        // Support reads a customer's Care card to help them; it never changes it. Care Ops (CARE/11) is support's job
        // too: working the exception queues and finding routes, inside the authority the customer already gave.
        [Role.CustomerSuccess] = [Permission.CustomerView, Permission.AssetView, Permission.CareView, Permission.CareOpsReview],

        // The insurer console read surface, confined to the insurer's own tenant by the InsurerUser mapping
        // (InsurerTenantGuard), not by customer ownership.
        [Role.Insurer] = [Permission.InsurerPortfolioView],

        [Role.SystemAdministrator] =
        [
            Permission.AdminManageUsers, Permission.AdminManageRoles, Permission.AdminViewLogs, Permission.CustomerView,
            // POPIA data-lifecycle actions.
            Permission.CustomerErase, Permission.CustomerExport,
            // Cross-tenant oversight, assigning a customer to an insurer tenant, and settling a deceased customer's
            // estate (passing their assets to an heir).
            Permission.InsurerPortfolioView, Permission.AssetTenantAssign, Permission.AssetEstateSettle,
        ],
    };

    public static bool Has(Role role, Permission permission)
        => Map.TryGetValue(role, out var granted) && granted.Contains(permission);

    /// <summary>Permissions are additive across roles: any role granting it suffices.</summary>
    public static bool HasAny(IEnumerable<Role> roles, Permission permission)
        => roles.Any(role => Has(role, permission));

    public static IReadOnlySet<Permission> For(Role role)
        => Map.TryGetValue(role, out var granted) ? granted : new HashSet<Permission>();
}
