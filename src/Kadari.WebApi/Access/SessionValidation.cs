using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Identity;

namespace Kadari.WebApi.Access;

/// <summary>
/// Rejects a structurally valid access token whose session is no longer live (PAS-002 §10).
/// </summary>
/// <remarks>
/// <para>
/// A JWT is stateless, so signature + expiry alone cannot express revocation: terminating a session
/// had no effect until the access token expired on its own. <c>Session.IsActive</c> is now consulted
/// on every authenticated request.
/// </para>
/// <para>
/// The lookup is cached for <see cref="CacheLifetime"/>, so the common path is not a database round
/// trip per request. That bounds the revocation lag to seconds rather than the token's lifetime —
/// a deliberate trade, and a far better one than the hour it replaced.
/// </para>
/// </remarks>
public static class SessionValidation
{
    /// <summary>Short enough that revocation is effectively immediate, long enough to absorb bursts.</summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    public static async Task EnsureSessionIsActiveAsync(TokenValidatedContext context)
    {
        var sessionClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;

        if (!Guid.TryParse(sessionClaim, out var sessionId))
        {
            // Tokens minted before sid existed, or a hand-crafted token omitting it. Fail closed:
            // a token we cannot tie to a session is a token we cannot revoke.
            context.Fail("The access token carries no session identifier.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();
        var clock = services.GetRequiredService<TimeProvider>();

        var isActive = await cache.GetOrCreateAsync($"session-active:{sessionId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheLifetime;

            var db = services.GetRequiredService<IKadariDbContext>();
            var session = await db.Set<Session>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == new SessionId(sessionId), context.HttpContext.RequestAborted);

            return session is not null && session.IsActive(clock.GetUtcNow());
        });

        if (!isActive)
        {
            context.Fail("The session backing this access token is no longer active.");
        }
    }
}
