namespace Kadari.Application.Exceptions;

/// <summary>
/// The action needs a consent the customer has not given (or has withdrawn). Mapped to HTTP 403 with the
/// stable <c>consent-required</c> code at the API edge, so a client can show the consent screen rather than a
/// generic "forbidden". <see cref="Scope"/> names what to ask for.
/// </summary>
public sealed class ConsentRequiredException(string scope)
    : Exception($"The customer has not consented to {scope}.")
{
    public string Scope { get; } = scope;
}
