using Kadari.Application.Common.Pagination;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.ListSessions;

internal sealed class ListSessionsHandler(IKadariDbContext db)
    : IQueryHandler<ListSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<IReadOnlyList<SessionDto>> Handle(ListSessionsQuery query, CancellationToken ct)
    {
        var accountId = new UserAccountId(query.UserAccountId);

        return await db.Set<Session>()
            .AsNoTracking()
            .Where(s => s.UserAccountId == accountId && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.LastActivityAt)
            .Select(s => new SessionDto(
                s.Id.Value, s.LoginAt, s.LastActivityAt, s.ExpiresAt, s.Status.ToString(), s.Device, s.IpAddress))
            // Naturally bounded — a person has a handful of devices — but bounded by the data is not
            // bounded by the query (PAS-002 §12). Newest first, so the cap drops the least interesting.
            .Take(PageRequest.MaxPageSize)
            .ToListAsync(ct);
    }
}
