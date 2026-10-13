using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.Subjects.Services;

/// <summary>
/// The Customer→Subject bridge (DES-001b): ensures a first-class <see cref="Subject"/> exists for an
/// onboarded customer and returns its id. Idempotent — one subject per customer. This is how the platform
/// adopts subjects going forward without re-pointing the existing Customer references: callers that have a
/// <see cref="CustomerId"/> resolve it to a <see cref="SubjectId"/> here.
/// </summary>
public sealed class SubjectRegistrar(IKadariDbContext db)
{
    public async Task<SubjectId> GetOrCreateForCustomerAsync(CustomerId customerId, CancellationToken ct)
    {
        var existing = await db.Subjects.FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new NotFoundException(nameof(Customer), customerId.Value);

        // A mobile-first customer (auto-registered from OTP) has no name yet, so fall back to the verified
        // mobile as the subject's human label until they complete their profile — Subject requires a non-empty
        // display name.
        var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
        var displayName = string.IsNullOrWhiteSpace(fullName) ? customer.MobileNumber : fullName;

        var subject = Subject.ForCustomer(customerId, displayName);
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(ct);

        return subject.Id;
    }
}
