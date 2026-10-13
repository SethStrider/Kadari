using MediatR;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Features.Identity.ListSessions;
using Kadari.Application.Features.Identity.TerminateSession;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// A user manages their own sessions (PCS-004 §8/§9). Requires a valid token; the caller's account
/// comes from the JWT, so a user can only see/terminate their own sessions.
/// </summary>
public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sessions").WithTags("Sessions").RequireAuthorization();

        group.MapGet("/", async (ICurrentUser user, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListSessionsQuery(user.AccountId!.Value), ct)))
            .WithName("ListSessions")
            .WithSummary("List the caller's active sessions.");

        group.MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new TerminateSessionCommand(id, user.AccountId!.Value), ct);
                return Results.NoContent();
            })
            .WithName("TerminateSession")
            .WithSummary("Terminate one of the caller's sessions (logout / revoke a device).");

        return group;
    }
}
