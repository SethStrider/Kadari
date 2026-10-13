namespace Kadari.Application.Abstractions.Access;

/// <summary>
/// The customer the current caller acts as, resolved from their account (PAS-002 §2).
/// </summary>
/// <remarks>
/// Resolution is a database lookup — <c>UserAccount.CustomerId</c> — so it is cached for the lifetime
/// of the request. Ownership is checked on potentially every scoped slice; without caching, the
/// authorization layer would add a query per request.
/// </remarks>
public interface ICurrentCustomer
{
    /// <returns>The caller's customer id, or <c>null</c> when the account is not linked to one.</returns>
    Task<Guid?> GetCustomerIdAsync(CancellationToken cancellationToken);
}
