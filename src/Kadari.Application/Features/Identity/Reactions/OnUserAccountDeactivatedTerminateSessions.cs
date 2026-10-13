using MediatR;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.Reactions;

/// <summary>
/// Ends every live session of an account that has been suspended, locked or erased (GL-12). Runs inside the
/// deactivating command's transaction, so the status change and the terminations commit together; access tokens
/// then stop working within <c>SessionValidation.CacheLifetime</c> and refresh tokens at once.
/// </summary>
internal sealed class OnUserAccountDeactivatedTerminateSessions(IKadariDbContext db, TimeProvider clock)
    : INotificationHandler<DomainEventNotification<UserAccountDeactivatedDomainEvent>>
{
    public async Task Handle(DomainEventNotification<UserAccountDeactivatedDomainEvent> notification, CancellationToken ct)
    {
        var accountId = notification.DomainEvent.UserAccountId;
        var now = clock.GetUtcNow();

        var sessions = await db.Set<Session>()
            .Where(s => s.UserAccountId == accountId && s.Status == SessionStatus.Active && s.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.Terminate();
        }

        await db.SaveChangesAsync(ct);
    }
}
