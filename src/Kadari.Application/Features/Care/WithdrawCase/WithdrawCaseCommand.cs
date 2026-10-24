using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.WithdrawCase;

/// <summary>The owner no longer needs the case. Customer-scoped.</summary>
public sealed record WithdrawCaseCommand(Guid CustomerId, Guid AssetId, Guid CaseId)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class WithdrawCaseHandler(CaseCommands cases) : ICommandHandler<WithdrawCaseCommand, CaseDto>
{
    public async Task<CaseDto> Handle(WithdrawCaseCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.Withdraw(cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
