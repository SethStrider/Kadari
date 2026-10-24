using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.AddCaseCommitment;

/// <summary>A promise with a date — "we'll call you within 5 working days" — that KADARI remembers and chases when it slips (CARE/14 C11). Customer-scoped.</summary>
public sealed record AddCaseCommitmentCommand(Guid CustomerId, Guid AssetId, Guid CaseId, string Party, string Description, DateOnly DueOn)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class AddCaseCommitmentHandler(CaseCommands cases) : ICommandHandler<AddCaseCommitmentCommand, CaseDto>
{
    public async Task<CaseDto> Handle(AddCaseCommitmentCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.AddCommitment(CareEnum.Parse<CommitmentParty>(cmd.Party), cmd.Description, cmd.DueOn, cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
