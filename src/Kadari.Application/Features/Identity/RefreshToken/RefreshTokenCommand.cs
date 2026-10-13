using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Features.Identity.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResponse>, IUnscopedRequest;

public sealed record RefreshTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);
