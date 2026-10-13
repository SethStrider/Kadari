using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Identity.Services;
using Kadari.Domain.Access;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.PasswordLogin;

/// <summary>
/// Email + password sign-in for the staff admin portal (portal.trustli.co.za). Unscoped — it *is* the
/// authentication step, so there is no signed-in caller yet to scope against.
/// </summary>
public sealed record PasswordLoginCommand(string Email, string Password)
    : ICommand<PasswordLoginResponse>, IUnscopedRequest;

public sealed record PasswordLoginResponse(
    string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, Guid UserAccountId);

internal sealed class PasswordLoginHandler(
    IKadariDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IRefreshTokenService refreshTokens,
    TimeProvider clock)
    : ICommandHandler<PasswordLoginCommand, PasswordLoginResponse>
{
    public async Task<PasswordLoginResponse> Handle(PasswordLoginCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var email = command.Email.Trim().ToLowerInvariant();

        var user = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Every rejection below throws the same generic exception so the response never reveals whether
        // the email exists, is staff, is locked or simply had the wrong password (no account enumeration).

        // Unknown email, or an account that has never set a password (OTP/social-only users).
        if (user is null || !user.HasPassword)
        {
            throw new InvalidCredentialsException();
        }

        // Staff-only portal: an account holding only self-service roles (Customer/Connector/Public) — or
        // none — may not sign in here, even with a valid password (PAS-002 §2).
        if (AccessPolicies.RequiresOwnershipScope(user.Roles))
        {
            throw new InvalidCredentialsException();
        }

        // A suspended/locked account, or one inside a brute-force lockout window, cannot sign in.
        if (user.Status != AccountStatus.Active || user.IsLockedOut(now))
        {
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(user.PasswordHash!, command.Password))
        {
            user.RegisterFailedLogin(now);
            // Persist the brute-force increment before rejecting: InvalidCredentialsException is an
            // IPersistableRejection, so the transaction commits this bump rather than rolling it back.
            await db.SaveChangesAsync(ct);
            throw new InvalidCredentialsException();
        }

        user.RegisterSuccessfulLogin(now);

        var refreshToken = refreshTokens.Generate();
        var session = Session.Start(user.Id, refreshTokens.Hash(refreshToken), now, now.Add(SessionLifetime.Refresh));
        db.Set<Session>().Add(session);

        await db.SaveChangesAsync(ct);

        var token = tokens.Issue(user, session.Id);
        return new PasswordLoginResponse(token.Token, token.ExpiresAt, refreshToken, user.Id.Value);
    }
}
