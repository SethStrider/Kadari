using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.GrantConsent;

/// <summary>
/// Record a customer's Kadari consent for a scope (spec §6, D8) — notably the distinct cross-border-AI
/// disclosure the capture pipeline checks before sending a home image off-shore. Customer-scoped: a caller
/// may only grant their own consent. Idempotent per (customer, scope): re-granting a withdrawn consent flips
/// it back rather than duplicating.
/// </summary>
public sealed record GrantKadariConsentCommand(Guid CustomerId, KadariConsentScope Scope)
    : ICommand<GrantKadariConsentResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record GrantKadariConsentResponse(Guid ConsentId, string Scope, string Status);

internal sealed class GrantKadariConsentHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<GrantKadariConsentCommand, GrantKadariConsentResponse>
{
    public async Task<GrantKadariConsentResponse> Handle(GrantKadariConsentCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);
        var now = clock.GetUtcNow();

        var consent = await db.KadariConsents
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Scope == cmd.Scope, ct);

        if (consent is null)
        {
            consent = KadariConsent.Grant(customerId, cmd.Scope, now);
            db.KadariConsents.Add(consent);
        }
        else
        {
            consent.Regrant(now);
        }

        await db.SaveChangesAsync(ct);

        return new GrantKadariConsentResponse(consent.Id.Value, consent.Scope.ToString(), consent.Status.ToString());
    }
}
