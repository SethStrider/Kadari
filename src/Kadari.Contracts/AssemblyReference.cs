namespace Kadari.Contracts;

/// <summary>
/// Marker type for locating the Kadari.Contracts assembly. Integration events live here: <c>sealed record</c>s of
/// primitives and ids only (no domain types, no EF entities), published through <c>IIntegrationEventPublisher</c>
/// and consumed in <c>Kadari.Worker</c>. A contract is versioned by adding a new record, never by changing a
/// published one, because messages already in the outbox or the broker were serialised against the old shape.
/// </summary>
public static class AssemblyReference;
