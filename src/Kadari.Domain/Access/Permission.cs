namespace Kadari.Domain.Access;

/// <summary>
/// The permission taxonomy (PRS-001 §4), named {Resource}{Action}. Governs every action; no page,
/// API or workflow may bypass a permission check.
/// </summary>
public enum Permission
{
    // Public
    BrowseCatalogue,
    VerifyReport,

    // Customers
    CustomerCreate,
    CustomerView,
    CustomerUpdate,
    CustomerArchive,
    CustomerErase,   // POPIA right-to-be-forgotten (PAS-002)
    CustomerExport,  // POPIA subject-access request (PAS-002)

    // Products
    ProductCreate,
    ProductPublish,
    ProductRetire,
    ProductPrice,
    ProductApprove,

    /// <summary>Break-glass: mint + publish a product version directly, bypassing the approval flow (PF-001). SysAdmin only.</summary>
    ProductPublishDirect,

    // DecisionSystems (Decision System Engine, DES-001)
    DecisionSystemManage,

    // OSINT — Digital Footprint (OSI-001)
    OsintCheckRequest,
    OsintCheckView,

    // Subjects & Decision Sets (DES-001b)
    SubjectManage,
    SubjectView,

    // Journeys
    JourneyStart,
    JourneyView,
    JourneyAssign,
    JourneyCancel,
    JourneyComplete,

    // Documents
    DocumentUpload,
    DocumentView,
    DocumentReplace,

    // Reports
    ReportGenerate,
    ReportApprove,
    ReportReturn,
    ReportView,
    ReportShare,
    ReportRevoke,
    ReportReissue,

    // References
    ReferenceRequest,
    ReferenceVerify,

    // Trust Profile
    TrustProfileView,
    TrustProfileShare,
    TrustProfileManagePermissions,

    // Payments
    PaymentCollect,
    PaymentRefund,
    PaymentInvoice,
    PaymentExport,
    PaymentView,

    // Connectors
    ConnectorRefer,
    ConnectorViewDashboard,
    ConnectorViewCommission,
    ConnectorApprove,
    ConnectorPayCommission,

    // Kadari (household asset intelligence)
    AssetCapture,
    AssetView,
    AssetValuate,
    AssetExport,
    AssetTransfer,
    InsurerPortfolioView,
    AssetTenantAssign,
    AssetEstateSettle,

    // Administration
    AdminManageUsers,
    AdminManageRoles,
    AdminSettings,
    AdminViewLogs,

    /// <summary>Manage the verification provider registry and rail routing (VER-001). Platform configuration.</summary>
    VerificationManage,

    /// <summary>
    /// Mark a customer's document verified or rejected (GL-04). Staff only: the owner may submit a document
    /// for review but never judge their own evidence. Appended so existing ordinals do not shift.
    /// </summary>
    DocumentVerify,
}
