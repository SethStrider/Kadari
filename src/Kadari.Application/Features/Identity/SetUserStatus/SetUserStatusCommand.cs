using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Audit;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.SetUserStatus;

/// <summary>
/// Suspend, re-activate, or lock a staff/user account (PRS-001 §3). The administrator lifecycle control
/// behind the portal's Users page. Additive to roles — this gates whether the account may act at all.
/// </summary>
// Unscoped: staff-only administration gated on AdminManageRoles — not a customer-owned resource.
public sealed record SetUserStatusCommand(Guid UserAccountId, AccountStatus Status)
    : ICommand<SetUserStatusResponse>, IUnscopedRequest;

public sealed record SetUserStatusResponse(Guid UserAccountId, string Status);

internal sealed class SetUserStatusHandler(IKadariDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<SetUserStatusCommand, SetUserStatusResponse>
{
    public async Task<SetUserStatusResponse> Handle(SetUserStatusCommand command, CancellationToken ct)
    {
        // Self-lockout guard: an administrator cannot suspend or lock their own account and strand
        // themselves out of the admin surface — mirrors the SystemAdministrator role guard.
        if (command.Status != AccountStatus.Active && currentUser.AccountId == command.UserAccountId)
        {
            throw new ConflictException(
                "An administrator cannot suspend or lock their own account. Ask another administrator.");
        }

        var id = new UserAccountId(command.UserAccountId);
        var user = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(UserAccount), command.UserAccountId);

        var previous = user.Status;
        switch (command.Status)
        {
            case AccountStatus.Active:
                user.Activate();
                break;
            case AccountStatus.Suspended:
                user.Suspend();
                break;
            case AccountStatus.Locked:
                user.Lock();
                break;
        }

        db.Set<AuditEvent>().Add(AuditEvent.Record(
            ActorKind.Account, subjectType: nameof(UserAccount), subjectId: command.UserAccountId,
            action: "UserStatusChanged", occurredAt: clock.GetUtcNow(), actorId: currentUser.AccountId,
            previousState: previous.ToString(), newState: user.Status.ToString()));
        await db.SaveChangesAsync(ct);

        return new SetUserStatusResponse(user.Id.Value, user.Status.ToString());
    }
}
