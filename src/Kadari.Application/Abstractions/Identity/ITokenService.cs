using Kadari.Domain.Identity;

namespace Kadari.Application.Abstractions.Identity;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Issues a signed access token (JWT) for an authenticated user.</summary>
public interface ITokenService
{
    /// <param name="sessionId">
    /// The session the token belongs to, carried as the <c>sid</c> claim so validation can check the
    /// session is still live (PAS-002 §10). A stateless token alone cannot be revoked, which left
    /// <c>TerminateSession</c> with no effect until the token expired.
    /// </param>
    AccessToken Issue(UserAccount user, SessionId sessionId);
}
