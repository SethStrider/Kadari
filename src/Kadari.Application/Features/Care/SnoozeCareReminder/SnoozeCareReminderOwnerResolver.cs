using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.SnoozeCareReminder;

/// <summary>A customer may only act on their own reminders — the command names the customer.</summary>
internal sealed class SnoozeCareReminderOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<SnoozeCareReminderCommand>
{
    public Task<ResourceOwner?> ResolveAsync(SnoozeCareReminderCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
