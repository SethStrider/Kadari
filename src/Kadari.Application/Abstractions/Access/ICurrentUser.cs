using Kadari.Domain.Access;

namespace Kadari.Application.Abstractions.Access;

/// <summary>
/// The identity acting on the current request. Stubbed for #5; Identity &amp; Auth (#7) supplies the
/// real implementation from the authenticated token.
/// </summary>
public interface ICurrentUser
{
    Guid? AccountId { get; }

    /// <summary>A user may hold several roles; permissions are additive across them (PCS-004 §14).</summary>
    IReadOnlyCollection<Role> Roles { get; }

    bool IsAuthenticated => AccountId is not null;
}
