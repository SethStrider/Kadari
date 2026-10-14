using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.DataRights;
using Kadari.Domain.Audit;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Customers.EraseCustomer;

/// <summary>
/// POPIA erasure / right to be forgotten: tombstone a customer's personal data in place (the customer record and
/// their login account) and erase their ledger (AS-03, see <see cref="KadariErasure"/>): assets, evidence files,
/// passports and consents. Records, ids and the immutable audit trail survive (nothing is hard-deleted, DB
/// standards); the person is simply no longer identifiable. An admin action gated by <c>CustomerErase</c>, so it is
/// <see cref="IUnscopedRequest"/> (it acts on any customer, not the caller's own resource). Idempotent.
/// </summary>
public sealed record EraseCustomerCommand(Guid CustomerId) : ICommand<EraseCustomerResponse>, IUnscopedRequest;

public sealed record EraseCustomerResponse(int AssetsErased, int AssetsHandedOver, int FilesDeleted);

internal sealed class EraseCustomerHandler(
    IKadariDbContext db,
    ICurrentUser currentUser,
    KadariErasure kadari,
    TimeProvider clock)
    : ICommandHandler<EraseCustomerCommand, EraseCustomerResponse>
{
    public async Task<EraseCustomerResponse> Handle(EraseCustomerCommand command, CancellationToken ct)
    {
        var customerId = new CustomerId(command.CustomerId);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new NotFoundException(nameof(Customer), command.CustomerId);

        customer.Anonymize();

        var account = await db.UserAccounts.FirstOrDefaultAsync(a => a.CustomerId == customerId, ct);
        account?.Anonymize();

        var now = clock.GetUtcNow();
        var kadariResult = await kadari.EraseAsync(customerId, now, ct);

        // POPIA erasures are themselves auditable events (the audit trail is de-identified, not deleted).
        db.Set<AuditEvent>().Add(AuditEvent.Record(
            ActorKind.Account, subjectType: "Customer", subjectId: command.CustomerId,
            action: "CustomerErased", occurredAt: now, actorId: currentUser.AccountId));

        await db.SaveChangesAsync(ct);

        return new EraseCustomerResponse(kadariResult.AssetsErased, kadariResult.AssetsHandedOver, kadariResult.FilesDeleted);
    }
}
