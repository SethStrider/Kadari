using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Identity;

/// <summary>Raised when a new user account is registered. Every account has a verified mobile number.</summary>
public sealed record UserRegisteredDomainEvent(UserAccountId UserAccountId, string MobileNumber) : IDomainEvent;
