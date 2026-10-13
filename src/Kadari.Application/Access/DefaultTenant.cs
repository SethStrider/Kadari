using Kadari.Application.Abstractions.Access;
using Kadari.Domain.Assets;

namespace Kadari.Application.Access;

/// <summary>
/// The fallback <see cref="ICurrentTenant"/>: every caller belongs to <see cref="TenantId.Default"/>, the
/// platform tenant that owns consumer-direct (Ambassador) users. This is the whole story for v1 — insurer
/// tenancy is a deferred fast-follow, at which point a host registers an insurer-aware resolver over this
/// (last registration wins), leaving the read paths untouched.
/// </summary>
internal sealed class DefaultTenant : ICurrentTenant
{
    public TenantId Current => TenantId.Default;
}
