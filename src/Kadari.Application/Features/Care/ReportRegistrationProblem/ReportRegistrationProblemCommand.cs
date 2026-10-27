using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.ReportRegistrationProblem;

/// <summary>
/// The owner says the registration did not work (CARE/04 C04 FAILED states). "The site was down" keeps it open to try
/// again; anything else needs a person (Care Ops). Either way the registration and its authority are kept, and the
/// failure is the provider's, not a claim that KADARI failed (CARE/07 §23). Customer-scoped.
/// </summary>
/// <param name="ProviderUnavailable">True when the official site or channel was unavailable — a retryable failure.</param>
public sealed record ReportRegistrationProblemCommand(
    Guid CustomerId, Guid AssetId, Guid RegistrationId, string Reason, bool ProviderUnavailable)
    : ICommand<RegistrationStatusResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record RegistrationStatusResponse(Guid RegistrationId, string Status);

internal sealed class ReportRegistrationProblemHandler(IKadariDbContext db, OwnedAssets assets, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ReportRegistrationProblemCommand, RegistrationStatusResponse>
{
    public async Task<RegistrationStatusResponse> Handle(ReportRegistrationProblemCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetAsync(cmd.CustomerId, cmd.AssetId, ct);
        var registration = await Registrations.GetAsync(db, asset, cmd.RegistrationId, ct);
        var now = clock.GetUtcNow();

        registration.Fail(cmd.Reason, retryable: cmd.ProviderUnavailable, now);
        db.Set<ExternalActionAudit>().Add(ExternalActionAudit.Record(
            asset.Id, ExternalActionType.WarrantyRegistration, registration.AuthorisationId, ExternalActor.Owner, user.AccountId,
            registration.Destination, "ProblemReported", now, result: registration.Status.ToString()));

        await db.SaveChangesAsync(ct);
        return new RegistrationStatusResponse(registration.Id.Value, registration.Status.ToString());
    }
}
