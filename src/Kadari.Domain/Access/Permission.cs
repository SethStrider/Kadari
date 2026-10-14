namespace Kadari.Domain.Access;

/// <summary>
/// The permission taxonomy, named {Resource}{Action}. Governs every action; no page, API or workflow may bypass a
/// permission check.
/// </summary>
public enum Permission
{
    // The asset ledger
    AssetView,
    AssetCapture,
    AssetValuate,
    AssetTransfer,
    AssetExport,
    AssetEstateSettle,
    AssetTenantAssign,

    // Insurers
    InsurerPortfolioView,

    // Customers and their data rights (POPIA)
    CustomerView,
    CustomerExport,
    CustomerErase,

    // Administration
    AdminManageUsers,
    AdminManageRoles,
    AdminViewLogs,
}
