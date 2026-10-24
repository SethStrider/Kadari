using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.AcceptCaseRoute;

/// <summary>The owner confirms an amber route (CARE/07 §9 AMBER: the owner confirms before anything is sent). Customer-scoped.</summary>
public sealed record AcceptCaseRouteCommand(Guid CustomerId, Guid AssetId, Guid CaseId)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class AcceptCaseRouteHandler(CaseCommands cases) : ICommandHandler<AcceptCaseRouteCommand, CaseDto>
{
    public async Task<CaseDto> Handle(AcceptCaseRouteCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.AcceptRoute(cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
