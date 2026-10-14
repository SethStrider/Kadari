using MediatR;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Access;
using Kadari.Domain.Customers;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Customers.Reactions;

/// <summary>
/// Auto-registration completes the account. A first-OTP verify provisions a <see cref="UserAccount"/> but no
/// profile, and every customer-scoped slice — and <c>/me</c> — resolves through the account's linked
/// <see cref="Customer"/> (PAS-002 §2). This reaction creates that anchor Customer from the verified mobile and
/// links it, in the <em>same transaction</em> as the registration (CLAUDE.md §6a — intra-boundary), so the
/// record exists the moment the caller is signed in (no worker, no race).
/// <para>
/// Guarded two ways: only <see cref="Role.Customer"/> accounts get a Customer (the same event is raised for
/// staff registration), and an account that already has one is left alone — so a re-run or a staff-first
/// account never double-provisions.
/// </para>
/// </summary>
internal sealed class OnUserRegisteredCreateCustomer(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<UserRegisteredDomainEvent>>
{
    public async Task Handle(DomainEventNotification<UserRegisteredDomainEvent> notification, CancellationToken ct)
    {
        var evt = notification.DomainEvent;

        var user = await db.UserAccounts.FirstOrDefaultAsync(u => u.Id == evt.UserAccountId, ct);
        if (user is null || user.CustomerId is not null || !user.Roles.Contains(Role.Customer))
        {
            return;
        }

        var customer = Customer.ProvisionForMobile(evt.MobileNumber, user.Country);
        db.Customers.Add(customer);
        user.LinkToCustomer(customer.Id);

        await db.SaveChangesAsync(ct);
    }
}
