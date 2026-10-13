using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Audit;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.AssignRole;

internal sealed class AssignRoleHandler(IKadariDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<AssignRoleCommand, AssignRoleResponse>
{
    public async Task<AssignRoleResponse> Handle(AssignRoleCommand command, CancellationToken ct)
    {
        var id = new UserAccountId(command.UserAccountId);

        var user = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(UserAccount), command.UserAccountId);

        user.AddRole(command.Role);   // idempotent on the aggregate — re-granting is a no-op, not an error

        // Granting a role is a security-relevant change — audit who granted what to whom (PAS-002).
        db.Set<AuditEvent>().Add(AuditEvent.Record(
            ActorKind.Account, subjectType: nameof(UserAccount), subjectId: command.UserAccountId,
            action: "RoleGranted", occurredAt: clock.GetUtcNow(),
            actorId: currentUser.AccountId, newState: command.Role.ToString()));
        await db.SaveChangesAsync(ct);

        return new AssignRoleResponse(user.Id.Value, [.. user.Roles.Select(role => role.ToString())]);
    }
}
