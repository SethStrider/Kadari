namespace Kadari.Application.Abstractions.Access;

/// <summary>
/// The request touches a customer-owned resource (PAS-002 §2). <c>ResourceAuthorizationBehavior</c>
/// resolves the owning customer and enforces ownership before the handler runs.
/// </summary>
/// <remarks>
/// <para>
/// Role permissions answer "may this kind of user do this kind of thing" — they cannot answer "is this
/// <em>their</em> report". Without the second question, any authenticated customer reaching an endpoint
/// their role permits could pass any id and read another customer's data.
/// </para>
/// <para>
/// Every request under <c>Features/</c> must declare this or <see cref="IUnscopedRequest"/>. The
/// choice is enforced by an architecture test, so a new slice cannot inherit the gap by omission.
/// </para>
/// </remarks>
public interface IResourceScopedRequest
{
    /// <summary>The id of the resource being accessed — a report id, journey id, customer id, …</summary>
    Guid ResourceId { get; }
}
