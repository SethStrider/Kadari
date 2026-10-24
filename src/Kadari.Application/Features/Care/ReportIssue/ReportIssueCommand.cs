using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.ReportIssue;

/// <summary>
/// "Something's wrong" (CARE/05 C10–C12, CARE/14 C7). The owner says what kind of problem it is and describes it. The
/// safety gate runs first — when it trips, the answer carries stop-use guidance and the case is kept for afterwards.
/// The case opens on the asset that already exists, the Routing Engine decides where it goes from the asset's own facts
/// and current cover, and the decision is recorded. Works from self-capture alone: with no purchase facts the route is
/// simply "a person will find the right place". Customer-scoped.
/// </summary>
public sealed record ReportIssueCommand(
    Guid CustomerId,
    Guid AssetId,
    string IssueClass,
    string Description,
    bool? OwnerSaysUnsafe) : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class ReportIssueHandler(IKadariDbContext db, OwnedAssets assets, CareRouting routing, TimeProvider clock)
    : ICommandHandler<ReportIssueCommand, CaseDto>
{
    public async Task<CaseDto> Handle(ReportIssueCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetAsync(cmd.CustomerId, cmd.AssetId, ct);
        var profile = await db.CareProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AssetId == asset.Id, ct)
            ?? throw new ConflictException("Turn on KADARI Care for this item before reporting a problem.");

        var now = clock.GetUtcNow();
        var safety = SafetyGate.Trips(cmd.Description, cmd.OwnerSaysUnsafe);
        var @case = AssetCase.Open(
            asset.Id, CareEnum.Parse<IssueClass>(cmd.IssueClass), cmd.Description, safety, profile.CoverageState, now);
        db.Set<AssetCase>().Add(@case);

        await routing.RouteAsync(asset, profile, @case, ct);
        await db.SaveChangesAsync(ct);

        return CaseView.Detail(@case, asset, DateOnly.FromDateTime(now.UtcDateTime));
    }
}
