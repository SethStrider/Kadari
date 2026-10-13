using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Identity;

public readonly record struct ExternalLoginId(Guid Value)
{
    public static ExternalLoginId New() => new(Guid.CreateVersion7());
}

/// <summary>The external identity providers a user may sign in with.</summary>
public enum ExternalLoginProvider
{
    Google,
    Facebook,
}

/// <summary>
/// A sign-in credential issued by an external provider, owned by a <see cref="UserAccount"/>.
/// <para>
/// Identity is the provider's <see cref="Subject"/> ("sub"), never the email. A subject is stable
/// across an email change — a Workspace domain migration reissues every address while the subject
/// stays put — so keying on email would greet a returning user as a stranger and fork their account.
/// </para>
/// <para>
/// <see cref="Email"/> is a snapshot taken when the link was made and is never rewritten: a different
/// email means a different provider account, which means a different subject and its own row. Rows are
/// revoked rather than deleted, so an unlinked credential stops authenticating but stays legible in
/// the account's history.
/// </para>
/// </summary>
public sealed class ExternalLogin : Entity<ExternalLoginId>
{
    private ExternalLogin() { }   // EF

    private ExternalLogin(
        ExternalLoginId id, UserAccountId userAccountId, ExternalLoginProvider provider,
        string subject, string? email, bool emailVerified, DateTimeOffset linkedAt) : base(id)
    {
        UserAccountId = userAccountId;
        Provider = provider;
        Subject = subject;
        Email = email;
        EmailVerified = emailVerified;
        LinkedAt = linkedAt;
    }

    public UserAccountId UserAccountId { get; private set; }

    public ExternalLoginProvider Provider { get; private set; }

    /// <summary>The provider's stable subject identifier. Unique per provider among active links.</summary>
    public string Subject { get; private set; } = null!;

    /// <summary>The email the provider reported at link time. Descriptive, never an identifier.</summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Whether the provider asserted the email was verified. Only a verified email may ever be used to
    /// match this credential to an existing account — an unverified one lets anyone who can type your
    /// address at a lax provider claim your account.
    /// </summary>
    public bool EmailVerified { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>A revoked link is history: it no longer authenticates.</summary>
    public bool IsActive => RevokedAt is null;

    /// <summary>Created only through <see cref="UserAccount"/> — the aggregate owns the credential set.</summary>
    internal static ExternalLogin Link(
        UserAccountId userAccountId, ExternalLoginProvider provider, string subject,
        string? email, bool emailVerified, DateTimeOffset linkedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return new ExternalLogin(
            ExternalLoginId.New(), userAccountId, provider, subject.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            emailVerified, linkedAt);
    }

    /// <summary>Revoked via <see cref="UserAccount.RevokeExternalLogin"/>, which guards the last credential.</summary>
    internal void Revoke(DateTimeOffset when)
    {
        if (IsActive)
        {
            RevokedAt = when;
        }
    }
}
