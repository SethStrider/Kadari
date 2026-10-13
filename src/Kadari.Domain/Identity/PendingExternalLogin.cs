using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Identity;

public readonly record struct PendingExternalLoginId(Guid Value)
{
    public static PendingExternalLoginId New() => new(Guid.CreateVersion7());
}

public enum PendingExternalLoginStatus
{
    Pending,
    Completed,
    Expired,
}

/// <summary>
/// A social sign-in that has been authenticated by the provider but cannot yet become a
/// <see cref="UserAccount"/>, because no account exists without a verified mobile number.
/// <para>
/// This is the holding pen between the OAuth callback and the OTP that follows it. Parking the
/// sign-in here — rather than creating a mobile-less account and reconciling later — is what removes
/// account merging from the design entirely: nothing is written to the identity spine until the mobile
/// proves who this is, so a returning WhatsApp customer signing in with Facebook for the first time
/// simply lands on the account they already had.
/// </para>
/// <para>
/// Short-lived and single-use, like <see cref="OtpChallenge"/>: its id is handed to the caller and is
/// the only thing tying the completed OTP back to the provider identity, so a stale or replayed one
/// must not still be spendable.
/// </para>
/// </summary>
public sealed class PendingExternalLogin : AggregateRoot<PendingExternalLoginId>
{
    private PendingExternalLogin() { }   // EF

    private PendingExternalLogin(
        PendingExternalLoginId id, ExternalLoginProvider provider, string subject,
        string? email, bool emailVerified, DateTimeOffset issuedAt, DateTimeOffset expiresAt) : base(id)
    {
        Provider = provider;
        Subject = subject;
        Email = email;
        EmailVerified = emailVerified;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        Status = PendingExternalLoginStatus.Pending;
    }

    public ExternalLoginProvider Provider { get; private set; }

    /// <summary>The provider's stable subject identifier, carried through to the resulting link.</summary>
    public string Subject { get; private set; } = null!;

    public string? Email { get; private set; }

    public bool EmailVerified { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public PendingExternalLoginStatus Status { get; private set; }

    /// <summary>The account the sign-in resolved to — existing or newly created. Set on completion.</summary>
    public UserAccountId? UserAccountId { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static PendingExternalLogin Issue(
        ExternalLoginProvider provider, string subject, string? email,
        bool emailVerified, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        if (expiresAt <= issuedAt)
        {
            throw new DomainException("A pending external login must expire after it is issued.");
        }

        return new PendingExternalLogin(
            PendingExternalLoginId.New(), provider, subject.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            emailVerified, issuedAt, expiresAt);
    }

    public bool IsPending(DateTimeOffset now) =>
        Status == PendingExternalLoginStatus.Pending && now < ExpiresAt;

    /// <summary>
    /// Spends this sign-in against the account the verified mobile resolved to. Single-use: a second
    /// attempt throws rather than binding the same provider identity to a second account.
    /// </summary>
    public void Complete(UserAccountId userAccountId, DateTimeOffset now)
    {
        if (Status == PendingExternalLoginStatus.Completed)
        {
            throw new DomainException("This social sign-in has already been completed.");
        }

        if (now >= ExpiresAt)
        {
            Status = PendingExternalLoginStatus.Expired;
            throw new DomainException("This social sign-in has expired — start it again.");
        }

        if (Status == PendingExternalLoginStatus.Expired)
        {
            throw new DomainException("This social sign-in has expired — start it again.");
        }

        Status = PendingExternalLoginStatus.Completed;
        UserAccountId = userAccountId;
        CompletedAt = now;
    }

    /// <summary>Marks an unspent sign-in expired, so a sweep can retire it without deleting the record.</summary>
    public void Expire()
    {
        if (Status == PendingExternalLoginStatus.Pending)
        {
            Status = PendingExternalLoginStatus.Expired;
        }
    }
}
