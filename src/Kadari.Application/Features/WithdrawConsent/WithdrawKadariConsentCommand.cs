using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.WithdrawConsent;

/// <summary>
/// Withdraw a Kadari consent (spec §6, POPIA s11(2)(b)) — each scope independently. Withdrawing
/// <see cref="KadariConsentScope.AssetImageProcessing"/> stops new captures; it does not erase what is already
/// held (that is the erasure request). Idempotent: withdrawing a scope never granted, or already withdrawn, is a
/// no-op. Customer-scoped: a caller may only withdraw their own consent.
/// </summary>
public sealed record WithdrawKadariConsentCommand(Guid CustomerId, KadariConsentScope Scope)
    : ICommand, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class WithdrawKadariConsentHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<WithdrawKadariConsentCommand>
{
    public async Task Handle(WithdrawKadariConsentCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);
        var consent = await db.KadariConsents
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Scope == cmd.Scope, ct);

        if (consent is not { IsActive: true })
        {
            return;
        }

        consent.Withdraw(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }
}
