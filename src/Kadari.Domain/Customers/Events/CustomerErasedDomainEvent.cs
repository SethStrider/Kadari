using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Customers;

/// <summary>Raised when a customer's personal data is erased under POPIA (PAS-002 — right to be forgotten).</summary>
public sealed record CustomerErasedDomainEvent(CustomerId CustomerId) : IDomainEvent;
