using Microsoft.Extensions.Options;

namespace Kadari.WebApi.Access;

/// <summary>
/// Where the refresh token lives between requests (GL-16): an HttpOnly, Secure cookie scoped to the auth routes,
/// so no script on the page can read it. A token stolen through XSS was good for the whole session; now only the
/// 15-minute access token is ever visible to JavaScript.
/// </summary>
public sealed class RefreshCookieOptions
{
    public const string SectionName = "RefreshCookie";

    /// <summary>
    /// Strict wherever the SPA and the API share an origin (production, the nginx containers). Development runs
    /// the Vite dev server cross-origin to the API, where only None (still Secure) is sent with the request.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;

    /// <summary>Matches the session lifetime; the session's own expiry is what actually ends it.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);
}

public static class RefreshCookie
{
    public const string Name = "trustli_refresh";

    /// <summary>Sent only to the auth routes, never with ordinary API calls.</summary>
    public const string Path = "/api/v1/auth";

    public static void Write(HttpContext context, string refreshToken) =>
        context.Response.Cookies.Append(Name, refreshToken, Options(context));

    public static string? Read(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(Name, Options(context));

    private static CookieOptions Options(HttpContext context)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RefreshCookieOptions>>().Value;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,   // browsers treat http://localhost as secure, so this holds in development too
            SameSite = settings.SameSite,
            Path = Path,
            MaxAge = settings.MaxAge,
            IsEssential = true,
        };
    }
}
