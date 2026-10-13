using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Exceptions;

/// <summary>
/// A rotated-out refresh token was presented again (GL-13). Rejected with 403, but the session's termination is
/// persisted — the whole point is that the thief's copy stops working too.
/// </summary>
public sealed class RefreshTokenReuseException()
    : ForbiddenException("Invalid refresh token."), IPersistableRejection;
