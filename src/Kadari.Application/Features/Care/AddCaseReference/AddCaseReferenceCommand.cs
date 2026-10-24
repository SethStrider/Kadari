using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.AddCaseReference;

/// <summary>The provider gave a reference number — added whenever it arrives (CARE/14 C9 "external reference can be added later"). Customer-scoped.</summary>
public sealed record AddCaseReferenceCommand(Guid CustomerId, Guid AssetId, Guid CaseId, string Party, string Reference)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class AddCaseReferenceHandler(CaseCommands cases) : ICommandHandler<AddCaseReferenceCommand, CaseDto>
{
    public async Task<CaseDto> Handle(AddCaseReferenceCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.AddReference(cmd.Party, cmd.Reference, cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
