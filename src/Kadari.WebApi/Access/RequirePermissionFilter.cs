using Kadari.Application.Abstractions.Access;
using Kadari.Application.Exceptions;
using Kadari.Domain.Access;

namespace Kadari.WebApi.Access;

/// <summary>
/// Endpoint filter enforcing that the current user's role grants a required <see cref="Permission"/>
/// (PRS-001). Unauthenticated → 401; authenticated without the permission → 403. Resource-scoped
/// checks (ownership/sharing via <see cref="AccessPolicies"/>) are applied in handlers on top of this.
/// </summary>
public sealed class RequirePermissionFilter(Permission permission) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!RolePermissions.HasAny(user.Roles, permission))
        {
            // Names the permission for our logs (GlobalExceptionHandler logs it at Warning). The caller
            // sees only the catalogue title: the `forbidden` entry does not expose exception messages,
            // because enumerating the permission model to an unauthorised caller tells them exactly
            // what to go phishing for.
            throw new ForbiddenException($"None of the caller's roles grant permission {permission}.");
        }

        return await next(context);
    }
}

/// <summary>
/// Endpoint metadata recording the permission an endpoint demands (PAS-002 §5). The filter alone is
/// invisible to OpenAPI; this makes the requirement describable — and greppable.
/// </summary>
public sealed record RequiredPermissionMetadata(Permission Permission);

public static class RequirePermissionExtensions
{
    /// <summary>Requires the caller's role to grant <paramref name="permission"/>.</summary>
    /// <remarks>
    /// The 401/403 responses are documented for every operation by <c>ProblemResponsesTransformer</c>,
    /// so they are not repeated here. The metadata is what
    /// <c>PermissionRequirementTransformer</c> turns into a security requirement.
    /// </remarks>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new RequirePermissionFilter(permission));
        builder.WithMetadata(new RequiredPermissionMetadata(permission));
        return builder;
    }
}
