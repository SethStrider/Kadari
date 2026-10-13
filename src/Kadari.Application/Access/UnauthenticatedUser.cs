using Kadari.Application.Abstractions.Access;
using Kadari.Domain.Access;

namespace Kadari.Application.Access;

/// <summary>
/// The fallback <see cref="ICurrentUser"/> for hosts that have no authenticated caller — the worker,
/// seeders, and tests that exercise a slice directly (PAS-002 §2).
/// </summary>
/// <remarks>
/// Deliberately holds no account and no roles, so it is refused by every ownership check. The
/// alternative — defaulting to a privileged actor so unattended hosts "just work" — is precisely the
/// fail-open pattern this work removed. Background commands are all
/// <see cref="IUnscopedRequest"/>, so the worker never needs more than this.
/// </remarks>
internal sealed class UnauthenticatedUser : ICurrentUser
{
    public Guid? AccountId => null;

    public IReadOnlyCollection<Role> Roles => [];
}
