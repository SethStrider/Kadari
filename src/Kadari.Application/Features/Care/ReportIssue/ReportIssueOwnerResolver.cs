using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.ReportIssue;

/// <summary>A customer may only report a problem on their own assets.</summary>
internal sealed class ReportIssueOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<ReportIssueCommand>
{
    public Task<ResourceOwner?> ResolveAsync(ReportIssueCommand request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
