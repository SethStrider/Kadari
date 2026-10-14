
namespace Kadari.Domain.Access;

/// <summary>
/// Resource-scoped authorization, layered on top of role permissions (PRS-001 §1: "further restricted
/// by ownership, assignment or organisational rules"). Pure — reused by handlers and the API filter.
/// </summary>
public static class AccessPolicies
{
    /// <summary>
    /// Roles whose reach is confined to their own resources. Everyone else is staff: their permission
    /// set is already narrow and deliberate, and their job requires acting across customers — a
    /// Verifier must open the journey they are verifying, and Customer Success must read the payment
    /// they are being asked about.
    /// </summary>
    private static readonly Role[] SelfServiceRoles = [Role.Customer, Role.PublicVisitor];

    /// <summary>
    /// Whether ownership must be proven for this caller, on top of the role permission check
    /// (PAS-002 §2). True when the caller holds only self-service roles.
    /// </summary>
    /// <remarks>
    /// A caller holding <em>no</em> roles is the least-trusted case, so it also requires ownership —
    /// the absence of a role must never read as an exemption.
    /// </remarks>
    public static bool RequiresOwnershipScope(IEnumerable<Role> roles)
        => roles.All(role => SelfServiceRoles.Contains(role));

    /// <summary>The owner may always access their own resource.</summary>
    public static bool OwnsResource(Guid? accessorAccountId, Guid ownerAccountId)
        => accessorAccountId is { } id && id == ownerAccountId;
}
