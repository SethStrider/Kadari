using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Identity;

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.CreateVersion7());
}

public enum SessionStatus
{
    Active,
    Terminated,
    Expired,
}

/// <summary>
/// A login session (PCS-004 §8), multi-device. Holds the refresh token (hashed) whose rotation
/// extends the session; the access token itself is stateless. Per-request revocation enforcement is
/// a later refinement — a terminated session's access token stays valid until it expires.
/// </summary>
public sealed class Session : AggregateRoot<SessionId>
{
    private Session() { }   // EF

    private Session(
        SessionId id, UserAccountId userAccountId, string refreshTokenHash,
        DateTimeOffset now, DateTimeOffset expiresAt, string? device, string? ipAddress) : base(id)
    {
        UserAccountId = userAccountId;
        RefreshTokenHash = refreshTokenHash;
        LoginAt = now;
        LastActivityAt = now;
        ExpiresAt = expiresAt;
        Status = SessionStatus.Active;
        Device = device;
        IpAddress = ipAddress;
    }

    public UserAccountId UserAccountId { get; private set; }
    public string RefreshTokenHash { get; private set; } = null!;

    /// <summary>
    /// The refresh token this session rotated away from. Presenting it again means two parties hold the
    /// session's tokens — one of them stolen — so the session is ended (GL-13).
    /// </summary>
    public string? PreviousRefreshTokenHash { get; private set; }
    public DateTimeOffset LoginAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public SessionStatus Status { get; private set; }
    public string? Device { get; private set; }
    public string? IpAddress { get; private set; }

    public static Session Start(
        UserAccountId userAccountId, string refreshTokenHash,
        DateTimeOffset now, DateTimeOffset expiresAt, string? device = null, string? ipAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);
        return new Session(SessionId.New(), userAccountId, refreshTokenHash, now, expiresAt, device, ipAddress);
    }

    public bool IsActive(DateTimeOffset now) => Status == SessionStatus.Active && now < ExpiresAt;

    /// <summary>Rotate the refresh token on use (extends activity). Only valid while active.</summary>
    public void Rotate(string newRefreshTokenHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newRefreshTokenHash);
        if (!IsActive(now))
        {
            throw new DomainException("A refresh token cannot be rotated on an inactive session.");
        }

        PreviousRefreshTokenHash = RefreshTokenHash;
        RefreshTokenHash = newRefreshTokenHash;
        LastActivityAt = now;
    }

    public void Terminate()
    {
        if (Status == SessionStatus.Active)
        {
            Status = SessionStatus.Terminated;
        }
    }
}
