using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;

namespace Kadari.Application.Features.Owners.Services;

/// <summary>
/// The Customer→Owner bridge (DES-001b): ensures a first-class <see cref="Owner"/> exists for an
/// onboarded customer and returns its id. Idempotent — one subject per customer. This is how the platform
/// adopts subjects going forward without re-pointing the existing Customer references: callers that have a
/// <see cref="CustomerId"/> resolve it to a <see cref="OwnerId"/> here.
/// </summary>
public sealed class OwnerRegistrar(IKadariDbContext db)
{
    public async Task<OwnerId> GetOrCreateForCustomerAsync(CustomerId customerId, CancellationToken ct)
    {
        var existing = await db.Owners.FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new NotFoundException(nameof(Customer), customerId.Value);

        // A mobile-first customer (auto-registered from OTP) has no name yet, so fall back to the verified
        // mobile as the subject's human label until they complete their profile — Owner requires a non-empty
        // display name.
        var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
        var displayName = string.IsNullOrWhiteSpace(fullName) ? customer.MobileNumber : fullName;

        var subject = Owner.ForCustomer(customerId, displayName);
        db.Owners.Add(subject);
        await db.SaveChangesAsync(ct);

        return subject.Id;
    }
}
