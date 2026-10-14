namespace Kadari.Domain.Access;

/// <summary>Who a KADARI user is. Roles are additive; permissions come from <see cref="RolePermissions"/>.</summary>
public enum Role
{
    /// <summary>Not signed in.</summary>
    PublicVisitor,

    /// <summary>A household building their own asset ledger.</summary>
    Customer,

    /// <summary>A professional valuer: an independent valuation lifts an asset to Value supported.</summary>
    Valuer,

    /// <summary>KADARI support staff.</summary>
    CustomerSuccess,

    /// <summary>An insurer's user, confined to their own tenant's customers.</summary>
    Insurer,

    /// <summary>Platform administration.</summary>
    SystemAdministrator,
}
