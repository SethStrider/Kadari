using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.ReportRegistrationProblem;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.CancelRegistration;

/// <summary>The owner changed their mind before confirming. The registration is kept, cancelled, and audited; its
/// authority is revoked so nothing further can be done under it. Customer-scoped.</summary>
public sealed record CancelRegistrationCommand(Guid CustomerId, Guid AssetId, Guid RegistrationId)
    : ICommand<RegistrationStatusResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class CancelRegistrationHandler(IKadariDbContext db, OwnedAssets assets, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CancelRegistrationCommand, RegistrationStatusResponse>
{
    public async Task<RegistrationStatusResponse> Handle(CancelRegistrationCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetAsync(cmd.CustomerId, cmd.AssetId, ct);
        var registration = await Registrations.GetAsync(db, asset, cmd.RegistrationId, ct);
        var authorisation = await db.Set<ExternalActionAuthorisation>().FirstAsync(a => a.Id == registration.AuthorisationId, ct);
        var now = clock.GetUtcNow();

        registration.Cancel(now);
        authorisation.Revoke(now);
        db.Set<ExternalActionAudit>().Add(ExternalActionAudit.Record(
            asset.Id, ExternalActionType.WarrantyRegistration, authorisation.Id, ExternalActor.Owner, user.AccountId,
            registration.Destination, "Cancelled", now, result: "Cancelled"));

        await db.SaveChangesAsync(ct);
        return new RegistrationStatusResponse(registration.Id.Value, registration.Status.ToString());
    }
}
