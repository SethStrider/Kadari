namespace Kadari.Domain.Abstractions;

/// <summary>
/// Thrown when an operation would break a domain invariant (e.g. an illegal state transition).
/// Distinct from Application-layer exceptions; mapped to a 409/422 at the API edge.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
