namespace Kadari.Domain.Access;

/// <summary>The core platform roles (PRS-001 §3).</summary>
public enum Role
{
    PublicVisitor,
    Customer,
    Connector,
    Verifier,
    QualityAssurance,
    CustomerSuccess,
    Finance,
    ProductAdministrator,
    SystemAdministrator,

    /// <summary>An insurer-console user (Kadari §27–28). Staff-like (not self-service): reads a tenant's
    /// cohort, confined to their own tenant by the InsurerUser mapping rather than by ownership.</summary>
    Insurer,
}
