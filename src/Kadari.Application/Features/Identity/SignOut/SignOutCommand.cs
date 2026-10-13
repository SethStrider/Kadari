using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.SignOut;

/// <summary>
/// Ends the session a refresh token belongs to (GL-16): the browser's sign-out. Unscoped, because the refresh
/// token is itself the proof of the session (it arrives in the HttpOnly cookie, not with an access token that
/// may already have expired). Idempotent: an unknown or already-ended token does nothing.
/// </summary>
public sealed record SignOutCommand(string RefreshToken) : ICommand, IUnscopedRequest;

internal sealed class SignOutHandler(IKadariDbContext db, IRefreshTokenService refreshTokens)
    : ICommandHandler<SignOutCommand>
{
    public async Task Handle(SignOutCommand command, CancellationToken ct)
    {
        var hash = refreshTokens.Hash(command.RefreshToken);
        var session = await db.Set<Session>()
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.Status == SessionStatus.Active, ct);
        if (session is null)
        {
            return;
        }

        session.Terminate();
        await db.SaveChangesAsync(ct);
    }
}
