using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.RefreshToken;

internal sealed class RefreshTokenHandler(
    IKadariDbContext db,
    ITokenService tokens,
    IRefreshTokenService refreshTokens,
    TimeProvider clock)
    : ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = refreshTokens.Hash(command.RefreshToken);

        var session = await db.Set<Session>().FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, ct);
        if (session is null)
        {
            // A token this session already rotated away from: whoever presents it second, one copy was stolen.
            // End the session for both and make the owner sign in again (GL-13).
            var reused = await db.Set<Session>().FirstOrDefaultAsync(s => s.PreviousRefreshTokenHash == hash, ct);
            if (reused is not null && reused.Status == SessionStatus.Active)
            {
                reused.Terminate();
                await db.SaveChangesAsync(ct);
                throw new RefreshTokenReuseException();
            }

            throw new ForbiddenException("Invalid refresh token.");
        }

        if (!session.IsActive(now))
        {
            throw new ForbiddenException("The session is no longer active.");
        }

        var user = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.Id == session.UserAccountId, ct)
            ?? throw new NotFoundException(nameof(UserAccount), session.UserAccountId.Value);

        // Belt and braces: deactivation already terminates sessions, but a session must never outlive its account.
        if (!user.CanSignIn)
        {
            throw new ForbiddenException("The account is not active.");
        }

        // Rotate: a used refresh token is replaced, so it can't be reused.
        var newRefreshToken = refreshTokens.Generate();
        session.Rotate(refreshTokens.Hash(newRefreshToken), now);
        await db.SaveChangesAsync(ct);

        var access = tokens.Issue(user, session.Id);
        return new RefreshTokenResponse(access.Token, access.ExpiresAt, newRefreshToken);
    }
}
