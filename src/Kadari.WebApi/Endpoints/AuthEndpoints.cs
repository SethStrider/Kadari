using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using Kadari.Application.Features.Identity.PasswordLogin;
using Kadari.Application.Features.Identity.RefreshToken;
using Kadari.Application.Features.Identity.RequestOtp;
using Kadari.Application.Features.Identity.SignOut;
using Kadari.Application.Features.Identity.VerifyOtp;
using Kadari.WebApi.Access;
using Kadari.WebApi.RateLimiting;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// Authentication endpoints (PCS-004): mobile → OTP → JWT. Public (these are the login flow).
/// OTP delivery is stubbed until #8; token *validation* / current-user wiring is 7c.
/// <para>
/// The refresh token never appears in a response body (GL-16): sign-in and refresh set it in the HttpOnly
/// <see cref="RefreshCookie"/>, and refresh / logout read it from there. Only the 15-minute access token is
/// visible to the SPA's JavaScript.
/// </para>
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        // Each request costs a real send, so the limit bounds enumeration across numbers — something
        // the handler's per-number resend window cannot see (PAS-002 §7).
        group.MapPost("/otp/request", async (RequestOtpCommand command, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(command, ct)))
            .RequireRateLimiting(RateLimitingExtensions.AuthOtpRequestPolicy)
            .WithName("RequestOtp")
            .WithSummary("Request an OTP for a mobile number (delivery stubbed until #8).");

        group.MapPost("/otp/verify", async (
                VerifyOtpCommand command, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                RefreshCookie.Write(http, result.RefreshToken);
                return Results.Ok(new SignInResponse(result.AccessToken, result.ExpiresAt, result.UserAccountId));
            })
            .Produces<SignInResponse>()
            .RequireRateLimiting(RateLimitingExtensions.AuthOtpVerifyPolicy)
            .WithName("VerifyOtp")
            .WithSummary("Verify an OTP: an access token in the body, the refresh token in an HttpOnly cookie (auto-registers a Customer).");

        // Staff admin-portal sign-in (portal.trustli.co.za). Public like the OTP flow — it *is* the
        // login. The handler refuses non-staff accounts and enforces per-account lockout; this policy
        // bounds cross-account guessing per source.
        group.MapPost("/login", async (
                PasswordLoginCommand command, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                RefreshCookie.Write(http, result.RefreshToken);
                return Results.Ok(new SignInResponse(result.AccessToken, result.ExpiresAt, result.UserAccountId));
            })
            .Produces<SignInResponse>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPasswordLoginPolicy)
            .WithName("PasswordLogin")
            .WithSummary("Sign in with email + password (staff only): an access token in the body, the refresh token in an HttpOnly cookie.");

        // The cookie is the token. A token in the body is still accepted so a browser that signed in before the
        // cookie existed (its token in localStorage) moves over on its next refresh instead of being signed out.
        group.MapPost("/refresh", async (
                RefreshRequest? body, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var token = RefreshCookie.Read(http) ?? body?.RefreshToken;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.Unauthorized();
                }

                var result = await sender.Send(new RefreshTokenCommand(token), ct);
                RefreshCookie.Write(http, result.RefreshToken);
                return Results.Ok(new RefreshResponse(result.AccessToken, result.ExpiresAt));
            })
            .RequireRateLimiting(RateLimitingExtensions.AuthRefreshPolicy)
            .Produces<RefreshResponse>()
            .WithName("RefreshToken")
            .WithSummary("Exchange the refresh cookie for a new access token (rotates the refresh token).");

        group.MapPost("/logout", async (ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (RefreshCookie.Read(http) is { } token)
                {
                    await sender.Send(new SignOutCommand(token), ct);
                }

                RefreshCookie.Clear(http);
                return Results.NoContent();
            })
            .RequireRateLimiting(RateLimitingExtensions.AuthRefreshPolicy)
            .WithName("Logout")
            .WithSummary("End this browser's session and clear its refresh cookie.");

        return group;
    }
}

/// <summary>A sign-in result as the SPA sees it: no refresh token (that is in the HttpOnly cookie).</summary>
public sealed record SignInResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserAccountId);

/// <summary>A refresh result as the SPA sees it: the new access token; the rotated refresh token is in the cookie.</summary>
public sealed record RefreshResponse(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>Only for a browser still holding a pre-cookie token in localStorage.</summary>
public sealed record RefreshRequest(string? RefreshToken);
