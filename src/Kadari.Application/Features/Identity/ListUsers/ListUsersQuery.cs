using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Common.Pagination;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.ListUsers;

/// <summary>
/// Staff user administration listing (PRS-001 §3) — the accounts an administrator manages roles on. Paged,
/// newest first, with an optional search over name / email / mobile. Backs the portal's Users page.
/// </summary>
// Unscoped: staff-only administration gated on AdminManageRoles — not a customer-owned resource.
public sealed record ListUsersQuery : PageRequest, IQuery<PagedResult<UserSummaryDto>>, IUnscopedRequest
{
    /// <summary>Case-insensitive match against first/last name, email, or mobile number.</summary>
    public string? Search { get; init; }
}

public sealed record UserSummaryDto(
    Guid Id,
    string Name,
    string? Email,
    string MobileNumber,
    string Status,
    IReadOnlyList<string> Roles);

internal sealed class ListUsersHandler(IKadariDbContext db)
    : IQueryHandler<ListUsersQuery, PagedResult<UserSummaryDto>>
{
    public async Task<PagedResult<UserSummaryDto>> Handle(ListUsersQuery query, CancellationToken ct)
    {
        var users = db.Set<UserAccount>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // lower(col) LIKE %term% — case-insensitive and provider-portable (no Npgsql-only ILike here in
            // Application). Names/email/mobile are all stored in plaintext, so the match is meaningful.
            var term = query.Search.Trim().ToLower();
            users = users.Where(u =>
                u.FirstName.ToLower().Contains(term)
                || u.LastName.ToLower().Contains(term)
                || (u.Email != null && u.Email.ToLower().Contains(term))
                || u.MobileNumber.ToLower().Contains(term));
        }

        var totalCount = await users.LongCountAsync(ct);

        // Newest accounts first; Roles is a value-converted collection, so the page is projected in memory.
        var page = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(ct);

        var items = page
            .Select(u => new UserSummaryDto(
                u.Id.Value,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.Email,
                u.MobileNumber,
                u.Status.ToString(),
                u.Roles.Select(r => r.ToString()).ToList()))
            .ToList();

        return PagedResult<UserSummaryDto>.From(items, query, totalCount);
    }
}
