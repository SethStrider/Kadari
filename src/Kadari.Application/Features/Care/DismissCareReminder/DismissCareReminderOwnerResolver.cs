using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.DismissCareReminder;

/// <summary>A customer may only act on their own reminders — the command names the customer.</summary>
internal sealed class DismissCareReminderOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<DismissCareReminderCommand>
{
    public Task<ResourceOwner?> ResolveAsync(DismissCareReminderCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
