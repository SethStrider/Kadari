namespace Kadari.Application.Exceptions;

/// <summary>
/// A per-resource budget is spent (e.g. OTP codes for one mobile number). Mapped to HTTP 429 at the API edge; the
/// message is not exposed, so a caller cannot probe the exact limits.
/// </summary>
public sealed class TooManyRequestsException(string message) : Exception(message);
