using MediatR;
using Kadari.Application.Common.Pagination;
using Kadari.Application.Features.Identity.AssignRole;
using Kadari.Application.Features.Identity.ListUsers;
using Kadari.Application.Features.Identity.RemoveRole;
using Kadari.Application.Features.Identity.SetUserStatus;
using Kadari.Domain.Access;
using Kadari.Domain.Identity;
using Kadari.WebApi.Access;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// User administration (PRS-001 §3): granting and revoking roles. Gated on
/// <see cref="Permission.AdminManageRoles"/>, which only <c>SystemAdministrator</c> holds.
/// <para>
/// Roles are additive, so these are grant/revoke operations on a set — never a replace. That is what
/// lets one account be both a Customer and an ambassador (<c>Connector</c>) with the union of both
/// permission sets.
/// </para>
/// <para>
/// The role is a route segment rather than a body field so that both verbs name it the same way. In a
/// JSON body an enum binds by <em>ordinal</em> (no <c>JsonStringEnumConverter</c> is configured), which
/// would make the grant contract <c>{"role": 2}</c> while the revoke contract said <c>Connector</c> —
/// and would silently change meaning if <see cref="Role"/> were ever reordered. Route values bind by
/// name, exactly as written in <see cref="Role"/>; an unknown or differently-cased name is a 400.
/// </para>
/// </summary>
public static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/users").WithTags("Administration").RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListUsersQuery query, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(query, ct)))
            .RequirePermission(Permission.AdminManageRoles)
            .WithName("ListUsers")
            .WithSummary("List staff/user accounts to administer (paged; search by name, email or mobile).")
            .Produces<PagedResult<UserSummaryDto>>();

        // PUT, not POST: granting is idempotent — re-granting a held role is a no-op, not a duplicate.
        group.MapPut("/{id:guid}/roles/{role}", async (
                Guid id, Role role, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new AssignRoleCommand(id, role), ct)))
            .RequirePermission(Permission.AdminManageRoles)
            .WithName("AssignRole")
            .WithSummary("Grant a role to a user account (additive — existing roles are kept).");

        group.MapDelete("/{id:guid}/roles/{role}", async (
                Guid id, Role role, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new RemoveRoleCommand(id, role), ct)))
            .RequirePermission(Permission.AdminManageRoles)
            .WithName("RemoveRole")
            .WithSummary("Revoke a role from a user account (an account must keep at least one).");

        // Status by name in the route (same reasoning as roles): stable, reorder-proof, unknown = 400.
        group.MapPut("/{id:guid}/status/{status}", async (
                Guid id, AccountStatus status, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SetUserStatusCommand(id, status), ct)))
            .RequirePermission(Permission.AdminManageRoles)
            .WithName("SetUserStatus")
            .WithSummary("Suspend, lock, or re-activate an account (an administrator cannot lock themselves out).");

        return group;
    }
}
