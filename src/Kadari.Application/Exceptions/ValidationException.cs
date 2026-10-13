namespace Kadari.Application.Exceptions;

/// <summary>
/// One or more request fields failed validation. Carries a per-field map of error messages.
/// Mapped to HTTP 400 (validation problem details) at the API edge.
/// </summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
