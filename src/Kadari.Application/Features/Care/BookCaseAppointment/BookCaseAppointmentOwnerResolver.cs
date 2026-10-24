using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.BookCaseAppointment;

/// <summary>A customer may only book an appointment on their own assets.</summary>
internal sealed class BookCaseAppointmentOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<BookCaseAppointmentCommand>
{
    public Task<ResourceOwner?> ResolveAsync(BookCaseAppointmentCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
