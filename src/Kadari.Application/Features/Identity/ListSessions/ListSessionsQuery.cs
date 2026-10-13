using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Features.Identity.ListSessions;

public sealed record ListSessionsQuery(Guid UserAccountId) : IQuery<IReadOnlyList<SessionDto>>, IUnscopedRequest;

public sealed record SessionDto(
    Guid Id,
    DateTimeOffset LoginAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt,
    string Status,
    string? Device,
    string? IpAddress);
