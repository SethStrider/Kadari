using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Customers.GetCurrentCustomer;

/// <summary>
/// The caller's own customer id and basic profile, resolved from their authenticated account. Unscoped by
/// design: there is no id in the request to own — authentication is the gate, and the answer is always the
/// caller's own record. This is how the client turns a token into the customer id its scoped calls need.
/// </summary>
public sealed record GetCurrentCustomerQuery : IQuery<CurrentCustomerResponse>, IUnscopedRequest;

public sealed record CurrentCustomerResponse(Guid CustomerId, string FirstName, string LastName, string MobileNumber);

internal sealed class GetCurrentCustomerHandler(ICurrentCustomer currentCustomer, IKadariDbContext db)
    : IQueryHandler<GetCurrentCustomerQuery, CurrentCustomerResponse>
{
    public async Task<CurrentCustomerResponse> Handle(GetCurrentCustomerQuery query, CancellationToken ct)
    {
        var customerId = await currentCustomer.GetCustomerIdAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), Guid.Empty);

        return await db.Customers.AsNoTracking()
            .Where(c => c.Id == new CustomerId(customerId))
            .Select(c => new CurrentCustomerResponse(c.Id.Value, c.FirstName, c.LastName, c.MobileNumber))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), customerId);
    }
}
