using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.GetCareReminders;

/// <summary>A customer may only read their own reminders — the query names the customer.</summary>
internal sealed class GetCareRemindersOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetCareRemindersQuery>
{
    public Task<ResourceOwner?> ResolveAsync(GetCareRemindersQuery request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
