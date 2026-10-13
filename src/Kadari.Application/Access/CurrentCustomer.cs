using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Identity;

namespace Kadari.Application.Access;

/// <summary>
/// Resolves the caller's customer from their account, once per request (PAS-002 §2).
/// </summary>
/// <remarks>
/// Registered scoped, and memoises the first lookup — including a negative result, so an account with
/// no linked customer does not re-query on every scoped slice in the same request.
/// </remarks>
internal sealed class CurrentCustomer(ICurrentUser currentUser, IKadariDbContext db) : ICurrentCustomer
{
    private bool _resolved;
    private Guid? _customerId;

    public async Task<Guid?> GetCustomerIdAsync(CancellationToken cancellationToken)
    {
        if (_resolved)
        {
            return _customerId;
        }

        if (currentUser.AccountId is { } accountId)
        {
            _customerId = await db.Set<UserAccount>()
                .AsNoTracking()
                .Where(u => u.Id == new UserAccountId(accountId))
                .Select(u => u.CustomerId!.Value.Value)
                .Cast<Guid?>()
                .FirstOrDefaultAsync(cancellationToken);
        }

        _resolved = true;
        return _customerId;
    }
}
