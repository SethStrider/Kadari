namespace Kadari.Application.Exceptions;

/// <summary>
/// The request conflicts with current state (e.g. a duplicate, or an illegal transition).
/// Mapped to HTTP 409 at the API edge.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
