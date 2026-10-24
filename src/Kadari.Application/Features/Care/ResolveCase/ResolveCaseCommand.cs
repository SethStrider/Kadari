using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.ResolveCase;

/// <summary>The case ended — repaired, replaced, refunded, credited, no fault found or rejected. The outcome is written to the asset's history (CARE/14 C9 "closed case writes outcome to asset history"). Customer-scoped.</summary>
public sealed record ResolveCaseCommand(Guid CustomerId, Guid AssetId, Guid CaseId, string Outcome, string? Notes)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class ResolveCaseHandler(CaseCommands cases) : ICommandHandler<ResolveCaseCommand, CaseDto>
{
    public async Task<CaseDto> Handle(ResolveCaseCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.Resolve(CareEnum.Parse<CaseOutcome>(cmd.Outcome), cmd.Notes, cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
