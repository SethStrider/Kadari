using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;

namespace Kadari.Application.Features.Care.GetCase;

/// <summary>One case with its timeline, route, references, promises, appointments, messages and — while it can still be
/// sent — the case pack the owner would share. Customer-scoped.</summary>
public sealed record GetCaseQuery(Guid CustomerId, Guid AssetId, Guid CaseId) : IQuery<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class GetCaseHandler(IKadariDbContext db, OwnedAssets assets, TimeProvider clock) : IQueryHandler<GetCaseQuery, CaseDto>
{
    public async Task<CaseDto> Handle(GetCaseQuery query, CancellationToken ct)
    {
        var asset = await assets.GetAsync(query.CustomerId, query.AssetId, ct);
        var @case = await CaseView.GetAsync(db, asset, query.CaseId, tracked: false, ct);
        return CaseView.Detail(@case, asset, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
    }
}
