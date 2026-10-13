namespace Kadari.Application.Exceptions;

/// <summary>
/// A requested aggregate/resource does not exist. Mapped to HTTP 404 at the API edge.
/// </summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}
