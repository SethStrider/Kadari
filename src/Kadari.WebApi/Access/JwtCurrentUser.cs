using System.Security.Claims;
using Kadari.Application.Abstractions.Access;
using Kadari.Domain.Access;

namespace Kadari.WebApi.Access;

/// <summary>
/// The real current user (#7c): reads the authenticated JWT principal — <c>sub</c> → AccountId and
/// role claims → Roles. Replaces StubCurrentUser. Requires JWT bearer auth to have run.
/// </summary>
public sealed class JwtCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? AccountId =>
        Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public IReadOnlyCollection<Role> Roles =>
        Principal?.FindAll(ClaimTypes.Role)
            .Select(c => Enum.TryParse<Role>(c.Value, out var role) ? role : (Role?)null)
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .ToArray()
        ?? [];
}
