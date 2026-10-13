namespace Kadari.Application.Features.Identity.Services;

/// <summary>How long a sign-in lasts before the user must sign in again, for every login path.</summary>
internal static class SessionLifetime
{
    /// <summary>
    /// Seven days (AS-08, was 30). The refresh token still sits in localStorage until GL-16 moves it into an
    /// HttpOnly cookie, so a token stolen through XSS is good for a week, not a month. The session's end is fixed
    /// at sign-in; refreshing does not extend it.
    /// </summary>
    public static readonly TimeSpan Refresh = TimeSpan.FromDays(7);
}
