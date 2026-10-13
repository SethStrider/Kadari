using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Domain.Access;

namespace Kadari.Application.Features.Identity.AssignRole;

/// <summary>
/// Grants a role to a user account (PRS-001 §3). Roles are additive (PCS-004 §14) — a Customer who is
/// also an ambassador holds both and their permissions are the union — so this never replaces the
/// account's existing roles.
/// </summary>
public sealed record AssignRoleCommand(Guid UserAccountId, Role Role) : ICommand<AssignRoleResponse>, IUnscopedRequest;

/// <summary>The account's full role set after the grant, as names (the wire contract is the name, not
/// the enum's ordinal — reordering <see cref="Role"/> must not silently change what a client reads).</summary>
public sealed record AssignRoleResponse(Guid UserAccountId, IReadOnlyCollection<string> Roles);
