using Kadari.Domain.Assets;

namespace Kadari.Application.Abstractions.Access;

/// <summary>
/// The tenant partition the current caller acts within (D2/D11) — the ambient scope Kadari reads narrow
/// on. A consumer-direct caller resolves to <see cref="TenantId.Default"/>; an insurer-scoped principal (a
/// deferred fast-follow) resolves to that insurer's tenant. Distinct from <see cref="ICurrentCustomer"/>:
/// who the caller is versus which data partition they belong to.
/// </summary>
public interface ICurrentTenant
{
    TenantId Current { get; }
}
