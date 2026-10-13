using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Domain.Access;

namespace Kadari.Application.Features.Identity.RemoveRole;

/// <summary>
/// Revokes a role from a user account (PRS-001 §3). The aggregate refuses to leave an account with no
/// role at all, and the handler refuses to let an administrator revoke their own administration.
/// </summary>
public sealed record RemoveRoleCommand(Guid UserAccountId, Role Role) : ICommand<RemoveRoleResponse>, IUnscopedRequest;

/// <summary>The account's remaining role set, as names (see <c>AssignRoleResponse</c> for why names).</summary>
public sealed record RemoveRoleResponse(Guid UserAccountId, IReadOnlyCollection<string> Roles);
