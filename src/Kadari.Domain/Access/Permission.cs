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

    // KADARI Care on an asset: read the card and coverage; enable Care, record purchase details, re-evaluate.
    CareView,
    CareManage,

    // Care Ops: work the exception queues — unknown routes, safety reviews, failed registrations — and resolve them.
    CareOpsReview,

    // Care metrics (CARE/14 C14): aggregate counts only — no customer, asset or message content.
    CareMetricsView,

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
