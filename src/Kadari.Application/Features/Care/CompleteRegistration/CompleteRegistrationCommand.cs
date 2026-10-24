using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.CompleteRegistration;

/// <summary>
/// The owner finished the official registration and records the provider's confirmation reference (CARE/05 C06). The
/// reference is the proof — a registration is complete only with it. It is kept as evidence on the asset, written to
/// the asset's history, audited, and the cover is re-worked out with the registration counted. Customer-scoped.
/// </summary>
public sealed record CompleteRegistrationCommand(
    Guid CustomerId, Guid AssetId, Guid RegistrationId, string ExternalReference, DateOnly RegisteredOn)
    : ICommand<CompleteRegistrationResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record CompleteRegistrationResponse(Guid RegistrationId, string Status, string CoverageState);

internal sealed class CompleteRegistrationHandler(
    IKadariDbContext db, OwnedAssets assets, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CompleteRegistrationCommand, CompleteRegistrationResponse>
{
    public async Task<CompleteRegistrationResponse> Handle(CompleteRegistrationCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.CustomerId, cmd.AssetId, ct);
        var registration = await Registrations.GetAsync(db, asset, cmd.RegistrationId, ct);
        var rule = await db.Set<RegistrationRuleVersion>().AsNoTracking().FirstAsync(r => r.Id == registration.RuleVersionId, ct);
        var provider = await db.Set<Brand>().AsNoTracking().Where(b => b.Id == rule.BrandId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        var now = clock.GetUtcNow();

        registration.Complete(cmd.ExternalReference, cmd.RegisteredOn, now);
        asset.Attach(Domain.Assets.AssetEvidence.RegistrationConfirmed(provider ?? "registration", registration.ExternalReference!, now));

        db.Set<ExternalActionAudit>().Add(ExternalActionAudit.Record(
            asset.Id, ExternalActionType.WarrantyRegistration, registration.AuthorisationId, ExternalActor.Owner, user.AccountId,
            registration.Destination, "Confirmed", now, result: "Completed", externalReference: registration.ExternalReference));

        await db.SaveChangesAsync(ct);

        // The completion reaction re-evaluated the cover in this transaction; report where it landed.
        var state = await db.CareProfiles.Where(p => p.AssetId == asset.Id).Select(p => p.CoverageState).FirstOrDefaultAsync(ct);
        return new CompleteRegistrationResponse(registration.Id.Value, registration.Status.ToString(), state.ToString());
    }
}
