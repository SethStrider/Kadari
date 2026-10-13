namespace Kadari.Application.Abstractions.Access;

/// <summary>Which kind of principal a resource belongs to.</summary>
public enum ResourceOwnerKind
{
    /// <summary>Owned by a customer — reports, journeys, documents, profiles, passports.</summary>
    Customer,

    /// <summary>
    /// Owned by a user account directly, with no customer in between — a connector's dashboard, a
    /// login session. A connector is a partner, not a customer, so customer-based ownership does not
    /// describe it.
    /// </summary>
    Account,
}

/// <summary>The principal a resource belongs to.</summary>
public readonly record struct ResourceOwner(Guid OwnerId, ResourceOwnerKind Kind)
{
    public static ResourceOwner Customer(Guid customerId) => new(customerId, ResourceOwnerKind.Customer);

    public static ResourceOwner Account(Guid accountId) => new(accountId, ResourceOwnerKind.Account);
}

/// <summary>
/// Resolves which principal owns the resource a request targets (PAS-002 §2). One implementation per
/// resource-scoped request, living in that request's slice folder.
/// </summary>
/// <remarks>
/// Ownership is a per-aggregate question — a report knows its customer directly, a task only through
/// its journey — so it is a small strategy rather than a single lookup the behaviour could hard-code.
/// </remarks>
public interface IResourceOwnerResolver<in TRequest> where TRequest : IResourceScopedRequest
{
    /// <returns>The owner, or <c>null</c> when the resource does not exist.</returns>
    Task<ResourceOwner?> ResolveAsync(TRequest request, CancellationToken cancellationToken);
}
