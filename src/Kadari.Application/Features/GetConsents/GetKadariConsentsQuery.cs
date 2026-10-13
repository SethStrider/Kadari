using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.GetConsents;

/// <summary>
/// Every Kadari consent scope with whether the customer currently grants it (spec §6) — what the capture screen
/// reads to decide whether to ask first. A scope never granted is reported as not granted, so the client sees
/// the full list it may need to ask for. Customer-scoped: a caller reads only their own consents.
/// </summary>
public sealed record GetKadariConsentsQuery(Guid CustomerId)
    : IQuery<IReadOnlyList<KadariConsentDto>>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record KadariConsentDto(string Scope, bool Granted, DateTimeOffset? GrantedAt, DateTimeOffset? WithdrawnAt);

internal sealed class GetKadariConsentsHandler(IKadariDbContext db)
    : IQueryHandler<GetKadariConsentsQuery, IReadOnlyList<KadariConsentDto>>
{
    public async Task<IReadOnlyList<KadariConsentDto>> Handle(GetKadariConsentsQuery query, CancellationToken ct)
    {
        var customerId = new CustomerId(query.CustomerId);
        var recorded = await db.KadariConsents.AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .ToDictionaryAsync(c => c.Scope, ct);

        return Enum.GetValues<KadariConsentScope>()
            .Select(scope => recorded.TryGetValue(scope, out var c)
                ? new KadariConsentDto(scope.ToString(), c.IsActive, c.IsActive ? c.GrantedAt : null, c.WithdrawnAt)
                : new KadariConsentDto(scope.ToString(), false, null, null))
            .ToList();
    }
}
