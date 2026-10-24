using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.SettleCaseAppointment;

/// <summary>A customer may only settle an appointment on their own assets.</summary>
internal sealed class SettleCaseAppointmentOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<SettleCaseAppointmentCommand>
{
    public Task<ResourceOwner?> ResolveAsync(SettleCaseAppointmentCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
