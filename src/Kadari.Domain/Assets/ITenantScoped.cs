namespace Kadari.Domain.Assets;

/// <summary>
/// Marks an Kadari aggregate as belonging to a tenant partition (D2/D11). The row-scoping key every
/// cross-cutting read narrows on, so an insurer cohort's data never bleeds into another tenant's console.
/// Consumer-direct data carries <see cref="TenantId.Default"/>.
/// </summary>
public interface ITenantScoped
{
    TenantId TenantId { get; }
}
