using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Audit;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.EnableCare;

/// <summary>
/// Switch KADARI Care on for an existing asset (CARE/14 C1) and work out its cover straight away. Additive: the
/// asset is untouched and no second asset is created — the profile references it by id, one per asset. Idempotent:
/// enabling again keeps the profile (correcting the category if it changed) and re-evaluates. Customer-scoped.
/// </summary>
/// <param name="Category">A <see cref="CareCategory"/> name ("MajorAppliance"). A name, not the enum, because JSON
/// enums bind by ordinal here and would silently change meaning if the enum were reordered.</param>
public sealed record EnableCareCommand(Guid CustomerId, Guid AssetId, string Category)
    : ICommand<EnableCareResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record EnableCareResponse(Guid AssetId, Guid CareProfileId, string CoverageState, bool Created);

internal sealed class EnableCareHandler(
    IKadariDbContext db, OwnedAssets assets, CareCoverage coverage, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<EnableCareCommand, EnableCareResponse>
{
    public async Task<EnableCareResponse> Handle(EnableCareCommand cmd, CancellationToken ct)
    {
        var category = Enum.Parse<CareCategory>(cmd.Category, ignoreCase: true);   // the validator guarantees a name
        var asset = await assets.GetForUpdateAsync(cmd.CustomerId, cmd.AssetId, ct);
        var profile = await db.CareProfiles.Include(p => p.Coverage).FirstOrDefaultAsync(p => p.AssetId == asset.Id, ct);
        var created = profile is null;
        var now = clock.GetUtcNow();

        if (profile is null)
        {
            profile = CareProfile.Enable(asset.Id, category, now);
            db.CareProfiles.Add(profile);
            db.Set<AuditEvent>().Add(AuditEvent.Record(
                ActorKind.Account, "Asset", asset.Id.Value, "CareEnabled", now, actorId: user.AccountId,
                newState: category.ToString()));
        }
        else
        {
            profile.Recategorise(category);
        }

        await coverage.EvaluateAsync(asset, profile, ct);
        await db.SaveChangesAsync(ct);

        return new EnableCareResponse(asset.Id.Value, profile.Id.Value, profile.CoverageState.ToString(), created);
    }
}
