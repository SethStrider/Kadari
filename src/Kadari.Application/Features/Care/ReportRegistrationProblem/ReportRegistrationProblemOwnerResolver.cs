using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.ReportRegistrationProblem;

/// <summary>A customer may only report problems with registrations on their own asset.</summary>
internal sealed class ReportRegistrationProblemOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<ReportRegistrationProblemCommand>
{
    public Task<ResourceOwner?> ResolveAsync(ReportRegistrationProblemCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
