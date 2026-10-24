using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.DismissOpportunity;

/// <summary>"Not now" on an extra-cover offer (CARE/05 C04). It is not offered again for this asset; re-evaluation
/// keeps it dismissed. Customer-scoped.</summary>
public sealed record DismissOpportunityCommand(Guid CustomerId, Guid AssetId, Guid OpportunityId)
    : ICommand<DismissOpportunityResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record DismissOpportunityResponse(Guid OpportunityId, string Status);

internal sealed class DismissOpportunityHandler(IKadariDbContext db, OwnedAssets assets, TimeProvider clock)
    : ICommandHandler<DismissOpportunityCommand, DismissOpportunityResponse>
{
    public async Task<DismissOpportunityResponse> Handle(DismissOpportunityCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetAsync(cmd.CustomerId, cmd.AssetId, ct);
        var opportunity = await db.Set<RegistrationOpportunity>()
            .FirstOrDefaultAsync(o => o.Id == new RegistrationOpportunityId(cmd.OpportunityId) && o.AssetId == asset.Id, ct)
            ?? throw new NotFoundException(nameof(RegistrationOpportunity), cmd.OpportunityId);

        opportunity.Dismiss(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new DismissOpportunityResponse(opportunity.Id.Value, opportunity.Status.ToString());
    }
}
