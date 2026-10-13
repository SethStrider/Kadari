namespace Kadari.Domain.Access;

/// <summary>
/// The role → permission grants (PRS-001 §3/§5), least-privilege. The single source of truth for
/// "which role may do what". Resource-scoped restrictions (ownership/sharing) are layered on top
/// via <see cref="AccessPolicies"/>.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<Role, HashSet<Permission>> Map = new Dictionary<Role, HashSet<Permission>>
    {
        [Role.PublicVisitor] = [Permission.BrowseCatalogue, Permission.VerifyReport],

        [Role.Customer] =
        [
            Permission.BrowseCatalogue, Permission.VerifyReport, Permission.JourneyStart,
            Permission.JourneyView, Permission.DocumentUpload, Permission.DocumentView,
            Permission.ReportView, Permission.ReportShare, Permission.TrustProfileView,
            Permission.TrustProfileShare, Permission.TrustProfileManagePermissions,
            Permission.PaymentInvoice, Permission.CustomerUpdate,
            // Kadari is customer self-service: capture into and view your own ledger, export an asset for
            // sale (the exit-to-PoziSnap seam), and transfer one you own to another person (deck §31).
            Permission.AssetCapture, Permission.AssetView, Permission.AssetExport, Permission.AssetTransfer,
        ],

        [Role.Connector] =
        [
            Permission.ConnectorRefer, Permission.ConnectorViewDashboard, Permission.ConnectorViewCommission,
        ],

        [Role.Verifier] =
        [
            Permission.JourneyView, Permission.DocumentUpload, Permission.DocumentView,
            Permission.ReferenceVerify, Permission.JourneyComplete,
            // Judging a customer's document is the verifier's call, never the owner's (GL-04).
            Permission.DocumentVerify,
            // Running a subject's footprint check is part of the verification legwork (OSI-001).
            Permission.OsintCheckRequest, Permission.OsintCheckView,
            // A verifier sees the subjects and decision sets they work against (DES-001b).
            Permission.SubjectView,
            // A professional (independent) asset valuation is verification legwork — it upgrades an asset's
            // assurance to ValueSupported, which the customer's own AI baseline can never reach (Kadari D10).
            Permission.AssetValuate,
        ],

        [Role.QualityAssurance] =
        [
            Permission.JourneyView, Permission.ReportApprove, Permission.ReportReturn, Permission.ReportView,
            // QA reviews the evidence a check produced, but does not initiate checks.
            Permission.OsintCheckView,
        ],

        [Role.CustomerSuccess] =
        [
            Permission.JourneyView, Permission.PaymentView, Permission.ReportReissue, Permission.CustomerView,
        ],

        [Role.Finance] =
        [
            Permission.PaymentCollect, Permission.PaymentRefund, Permission.PaymentInvoice,
            Permission.PaymentExport, Permission.PaymentView, Permission.ConnectorViewCommission,
            Permission.ConnectorPayCommission,
        ],

        [Role.Insurer] =
        [
            // The insurer console read surface (Kadari §27–28). Confined to the insurer's own tenant by
            // the InsurerUser mapping (InsurerTenantGuard), not by customer ownership.
            Permission.InsurerPortfolioView,
        ],

        [Role.ProductAdministrator] =
        [
            Permission.ProductCreate, Permission.ProductPublish, Permission.ProductRetire, Permission.ProductPrice,
            // Approval governance (PF-001): a ProductAdministrator may also approve — the submitter≠approver
            // invariant means two of them maker-check each other.
            Permission.ProductApprove,
            Permission.DecisionSystemManage,
        ],

        [Role.SystemAdministrator] =
        [
            Permission.AdminManageUsers, Permission.AdminManageRoles, Permission.AdminSettings,
            Permission.AdminViewLogs, Permission.ConnectorApprove, Permission.CustomerView,
            // Product approval + break-glass direct publish (PF-001) — the only role that bypasses approval.
            Permission.ProductApprove, Permission.ProductPublishDirect,
            // The enterprise subject model is administered here (DES-001b).
            Permission.SubjectManage, Permission.SubjectView,
            // POPIA data-lifecycle actions (PAS-002).
            Permission.CustomerErase, Permission.CustomerExport,
            // Insurer/tenant oversight (Kadari §27–28) — cross-customer portfolio reads. A dedicated
            // insurer role grants this too once insurer principals exist; for now it is admin oversight.
            Permission.InsurerPortfolioView,
            // Assigning a customer's cohort to an insurer tenant (Kadari D11) is an administrative action.
            Permission.AssetTenantAssign,
            // Settling a deceased customer's estate — passing their assets to an heir (Kadari D3).
            Permission.AssetEstateSettle,
            // The verification provider registry + rail routing is platform configuration (VER-001).
            Permission.VerificationManage,
        ],
    };

    public static bool Has(Role role, Permission permission)
        => Map.TryGetValue(role, out var granted) && granted.Contains(permission);

    /// <summary>Permissions are additive across roles (PCS-004 §14): any role granting it suffices.</summary>
    public static bool HasAny(IEnumerable<Role> roles, Permission permission)
        => roles.Any(role => Has(role, permission));

    public static IReadOnlySet<Permission> For(Role role)
        => Map.TryGetValue(role, out var granted) ? granted : new HashSet<Permission>();
}
