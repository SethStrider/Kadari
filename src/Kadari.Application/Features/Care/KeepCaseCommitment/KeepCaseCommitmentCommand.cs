using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.KeepCaseCommitment;

/// <summary>A promise was kept. Customer-scoped.</summary>
public sealed record KeepCaseCommitmentCommand(Guid CustomerId, Guid AssetId, Guid CaseId, Guid CommitmentId)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class KeepCaseCommitmentHandler(CaseCommands cases) : ICommandHandler<KeepCaseCommitmentCommand, CaseDto>
{
    public async Task<CaseDto> Handle(KeepCaseCommitmentCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.KeepCommitment(new CaseCommitmentId(cmd.CommitmentId), cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
