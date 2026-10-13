using Kadari.Application.Abstractions.Access;
using Kadari.Domain.Access;

namespace Kadari.WebApi.Access;

/// <summary>
/// TEMPORARY current-user for #5: reads the acting role/account from debug headers so the permission
/// model can be exercised end-to-end before real auth exists. Replaced by a token-backed implementation
/// in Identity &amp; Auth (#7). Headers: <c>X-Debug-Account</c> (GUID), <c>X-Debug-Role</c> (role name).
/// </summary>
public sealed class StubCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? AccountId =>
        Guid.TryParse(accessor.HttpContext?.Request.Headers["X-Debug-Account"].ToString(), out var id)
            ? id
            : null;

    /// <summary>Comma-separated role names in <c>X-Debug-Role</c> (e.g. "Customer,Connector").</summary>
    public IReadOnlyCollection<Role> Roles
    {
        get
        {
            var parsed = (accessor.HttpContext?.Request.Headers["X-Debug-Role"].ToString() ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => Enum.TryParse<Role>(name, ignoreCase: true, out var role) ? role : (Role?)null)
                .Where(role => role is not null)
                .Select(role => role!.Value)
                .ToArray();

            return parsed.Length > 0 ? parsed : [Role.PublicVisitor];
        }
    }
}
