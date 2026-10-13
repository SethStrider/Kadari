using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Identity;

/// <summary>
/// Raised when an account stops being allowed to act — suspended, locked or erased (GL-12). Its live sessions
/// must end, or a suspension takes effect only when the refresh token expires, 30 days later.
/// </summary>
public sealed record UserAccountDeactivatedDomainEvent(UserAccountId UserAccountId, AccountStatus Status) : IDomainEvent;
