using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Customers;

/// <summary>Raised when a new <see cref="Customer"/> is registered.</summary>
public sealed record CustomerRegisteredDomainEvent(CustomerId CustomerId) : IDomainEvent;
