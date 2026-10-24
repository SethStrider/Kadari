using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.RecordProviderUpdate;

/// <summary>The provider replied or moved the case on, captured by the owner until integrations exist (CARE/14 C10 "provider reply may be manually captured"). Customer-scoped.</summary>
public sealed record RecordProviderUpdateCommand(Guid CustomerId, Guid AssetId, Guid CaseId, string Status, string? Note)
    : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class RecordProviderUpdateHandler(CaseCommands cases) : ICommandHandler<RecordProviderUpdateCommand, CaseDto>
{
    public async Task<CaseDto> Handle(RecordProviderUpdateCommand cmd, CancellationToken ct)
    {
        var (asset, c) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        c.RecordProviderUpdate(CareEnum.Parse<CaseStatus>(cmd.Status), cmd.Note, cases.Now);
        return await cases.SaveAsync(asset, c, ct);
    }
}
