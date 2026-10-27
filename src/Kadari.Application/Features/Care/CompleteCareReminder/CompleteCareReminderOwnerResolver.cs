using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.CompleteCareReminder;

/// <summary>A customer may only act on their own reminders — the command names the customer.</summary>
internal sealed class CompleteCareReminderOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<CompleteCareReminderCommand>
{
    public Task<ResourceOwner?> ResolveAsync(CompleteCareReminderCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
