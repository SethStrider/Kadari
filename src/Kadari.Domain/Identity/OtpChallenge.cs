using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Identity;

public readonly record struct OtpChallengeId(Guid Value)
{
    public static OtpChallengeId New() => new(Guid.CreateVersion7());
}

public enum OtpStatus
{
    Pending,
    Verified,
    Expired,
    Failed,
}

public enum OtpVerification
{
    Success,
    Expired,
    Invalid,
    TooManyAttempts,
    AlreadyResolved,
}

/// <summary>
/// A one-time-passcode challenge for a mobile number (PCS-004 §6/§12). Stores the code <em>hashed</em>
/// and enforces expiry + brute-force limits. The code is generated/hashed outside the domain; the
/// aggregate only compares hashes and owns the state transitions.
/// </summary>
public sealed class OtpChallenge : AggregateRoot<OtpChallengeId>
{
    public const int MaxAttempts = 5;

    private OtpChallenge() { }   // EF

    private OtpChallenge(OtpChallengeId id, string mobileNumber, string codeHash, DateTimeOffset expiresAt)
        : base(id)
    {
        MobileNumber = mobileNumber;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
        Status = OtpStatus.Pending;
    }

    public string MobileNumber { get; private set; } = null!;
    public string CodeHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public int Attempts { get; private set; }
    public OtpStatus Status { get; private set; }

    public static OtpChallenge Issue(string mobileNumber, string codeHash, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        return new OtpChallenge(OtpChallengeId.New(), mobileNumber.Trim(), codeHash, expiresAt);
    }

    public OtpVerification Verify(string providedCodeHash, DateTimeOffset now)
    {
        if (Status != OtpStatus.Pending)
        {
            return OtpVerification.AlreadyResolved;
        }

        if (now >= ExpiresAt)
        {
            Status = OtpStatus.Expired;
            return OtpVerification.Expired;
        }

        Attempts++;

        if (!string.Equals(providedCodeHash, CodeHash, StringComparison.Ordinal))
        {
            if (Attempts >= MaxAttempts)
            {
                Status = OtpStatus.Failed;
                return OtpVerification.TooManyAttempts;
            }

            return OtpVerification.Invalid;
        }

        Status = OtpStatus.Verified;
        return OtpVerification.Success;
    }
}
