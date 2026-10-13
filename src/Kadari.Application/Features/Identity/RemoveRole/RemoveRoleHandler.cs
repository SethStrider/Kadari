using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Access;
using Kadari.Domain.Audit;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.RemoveRole;

internal sealed class RemoveRoleHandler(IKadariDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RemoveRoleCommand, RemoveRoleResponse>
{
    public async Task<RemoveRoleResponse> Handle(RemoveRoleCommand command, CancellationToken ct)
    {
        // Lock-out guard. AdminManageRoles is granted by SystemAdministrator alone, so an administrator
        // who drops their own copy of it can no longer reach this endpoint to get it back — and on a
        // single-administrator platform that leaves nobody who can. Revoking it from *someone else* is
        // fine; this only refuses the self-inflicted case.
        if (command.Role == Role.SystemAdministrator && currentUser.AccountId == command.UserAccountId)
        {
            throw new ConflictException(
                "An administrator cannot revoke their own SystemAdministrator role. Ask another administrator.");
        }

        var id = new UserAccountId(command.UserAccountId);

        var user = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(UserAccount), command.UserAccountId);

        user.RemoveRole(command.Role);   // DomainException (→ 409) if this is the account's last role

        db.Set<AuditEvent>().Add(AuditEvent.Record(
            ActorKind.Account, subjectType: nameof(UserAccount), subjectId: command.UserAccountId,
            action: "RoleRevoked", occurredAt: clock.GetUtcNow(),
            actorId: currentUser.AccountId, previousState: command.Role.ToString()));
        await db.SaveChangesAsync(ct);

        return new RemoveRoleResponse(user.Id.Value, [.. user.Roles.Select(role => role.ToString())]);
    }
}
