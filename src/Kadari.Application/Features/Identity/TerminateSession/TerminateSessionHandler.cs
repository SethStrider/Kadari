using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.TerminateSession;

internal sealed class TerminateSessionHandler(IKadariDbContext db)
    : ICommandHandler<TerminateSessionCommand>
{
    public async Task Handle(TerminateSessionCommand command, CancellationToken ct)
    {
        var sessionId = new SessionId(command.SessionId);

        var session = await db.Set<Session>().FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new NotFoundException(nameof(Session), command.SessionId);

        // A caller may only terminate their own sessions.
        if (session.UserAccountId.Value != command.UserAccountId)
        {
            throw new ForbiddenException("You can only terminate your own sessions.");
        }

        session.Terminate();
        await db.SaveChangesAsync(ct);
    }
}
