namespace Kadari.Application.Exceptions;

/// <summary>
/// The caller is authenticated but lacks the required permission (or resource-scoped access).
/// Mapped to HTTP 403 at the API edge.
/// </summary>
public class ForbiddenException(string message) : Exception(message);
