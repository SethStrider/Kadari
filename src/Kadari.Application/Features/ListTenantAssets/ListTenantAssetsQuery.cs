using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Common.Pagination;

namespace Kadari.Application.Features.ListTenantAssets;

/// <summary>
/// List the assets in an insurer tenant's cohort, paged (deck §27–28) — the drill-down beside the portfolio
/// summary. Cross-customer oversight, not customer-scoped, so gated at the edge by
/// <c>Permission.InsurerPortfolioView</c> rather than ownership (<see cref="IUnscopedRequest"/>). Assurance and
/// best value are derived per row; sorting is whitelisted to SQL-backed columns (name, registration, category).
/// </summary>
public sealed record ListTenantAssetsQuery : PageRequest, IQuery<PagedResult<TenantAssetRowDto>>, IUnscopedRequest
{
    public Guid TenantId { get; init; }
}

/// <summary>One row of the insurer cohort list: the asset, its owning policyholder, and the derived headline.</summary>
public sealed record TenantAssetRowDto(
    Guid AssetId,
    Guid OwnerId,
    string DisplayName,
    string Category,
    string Assurance,
    long? BestValueMinor,
    string? Currency,
    string Status,
    string? RoomLabel);
