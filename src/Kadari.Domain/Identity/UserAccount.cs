using Kadari.Domain.Abstractions;
using Kadari.Domain.Access;
using Kadari.Domain.Customers;

namespace Kadari.Domain.Identity;

public readonly record struct UserAccountId(Guid Value)
{
    public static UserAccountId New() => new(Guid.CreateVersion7());
}

public enum AccountStatus
{
    Active,
    Locked,
    Suspended,
}

/// <summary>
/// The master identity record (PCS-004 §10) — separate from <see cref="Customer"/>/TrustProfile.
/// A user may hold several roles (§14, additive) and links to a <see cref="Customer"/> and/or a
/// a connector depending on what they are.
/// <para>
/// <b>One account per human, permanently.</b> A verified mobile number is what identifies that human:
/// no account exists without one, and every sign-in path converges on it. An <see cref="ExternalLogin"/>
/// is only ever a secondary credential hanging off an account the mobile already established, so
/// gaining a social login never forks the account and never splits a customer's order history or an
/// ambassador's commission across two rows.
/// </para>
/// <para>
/// A first-time social sign-in therefore does <em>not</em> create an account. It parks in a
/// <see cref="PendingExternalLogin"/> until the mobile is verified, at which point it either attaches
/// to the existing account for that number or creates one alongside it.
/// </para>
/// </summary>
public sealed class UserAccount : AggregateRoot<UserAccountId>
{
    /// <summary>Consecutive failed password logins before the account is temporarily locked (PAS-002 §7).</summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>How long a password lockout holds before sign-in may be attempted again.</summary>
    public static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(15);

    private readonly List<Role> _roles = [];
    private readonly List<ExternalLogin> _externalLogins = [];

    private UserAccount() { }   // EF

    private UserAccount(
        UserAccountId id, string mobileNumber, string firstName, string lastName,
        string country, Role initialRole, string? email) : base(id)
    {
        MobileNumber = mobileNumber;
        FirstName = firstName;
        LastName = lastName;
        Country = country;
        Email = email;
        Status = AccountStatus.Active;
        _roles.Add(initialRole);

        RaiseDomainEvent(new UserRegisteredDomainEvent(id, mobileNumber));
    }

    /// <summary>The verified mobile number that identifies this human. Unique, and always present.</summary>
    public string MobileNumber { get; private set; } = null!;

    /// <summary>The account's primary email. Unique where present.</summary>
    public string? Email { get; private set; }

    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string Country { get; private set; } = null!;
    public string? PreferredLanguage { get; private set; }
    public string? TimeZone { get; private set; }
    public AccountStatus Status { get; private set; }
    public CustomerId? CustomerId { get; private set; }


    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>
    /// The account's password credential, <em>hashed</em> — null until one is set. Hashing/verification
    /// happens outside the domain (an <c>IPasswordHasher</c>), exactly as OTP codes do: the aggregate
    /// only stores the opaque hash and owns the lockout state around it. Passwordless (OTP/social) accounts
    /// simply never set one; the staff portal login is the path that requires it.
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Consecutive failed password attempts since the last success — the brute-force counter.</summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>When set and still in the future, password sign-in is refused (a lockout is in effect).</summary>
    public DateTimeOffset? LockedOutUntil { get; private set; }

    /// <summary>Whether this account can be signed in with a password at all.</summary>
    public bool HasPassword => PasswordHash is not null;

    public IReadOnlyList<Role> Roles => _roles;

    /// <summary>Every external credential ever linked, revoked ones included (they are history).</summary>
    public IReadOnlyList<ExternalLogin> ExternalLogins => _externalLogins;

    public static UserAccount Register(
        string mobileNumber, string firstName, string lastName, string country,
        Role initialRole = Role.Customer, string? email = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        return new UserAccount(
            UserAccountId.New(), mobileNumber.Trim(), firstName.Trim(), lastName.Trim(),
            country.Trim().ToUpperInvariant(), initialRole, Normalise(email));
    }

    /// <summary>
    /// Mobile-first provisioning (PCS-004): create a Customer account from just a verified mobile.
    /// Name/profile are completed later. Used by the auto-register-on-first-OTP flow.
    /// </summary>
    public static UserAccount ProvisionForMobile(string mobileNumber, string country = "ZA")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        return new UserAccount(
            UserAccountId.New(), mobileNumber.Trim(), string.Empty, string.Empty,
            country.Trim().ToUpperInvariant(), Role.Customer, email: null);
    }

    /// <summary>
    /// Completes a first-time social sign-in for a mobile number that has no account yet: the number is
    /// already verified by then (the OTP gate), so the account and its first external credential are
    /// created together. Where the number <em>does</em> already have an account, the caller links to
    /// that one via <see cref="LinkExternalLogin"/> instead — which is what keeps one human on one account.
    /// </summary>
    public static UserAccount RegisterWithExternalLogin(
        string mobileNumber, ExternalLoginProvider provider, string subject, string? email,
        bool emailVerified, DateTimeOffset linkedAt, string country = "ZA")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var account = new UserAccount(
            UserAccountId.New(), mobileNumber.Trim(), string.Empty, string.Empty,
            country.Trim().ToUpperInvariant(), Role.Customer, Normalise(email));

        account._externalLogins.Add(
            ExternalLogin.Link(account.Id, provider, subject, email, emailVerified, linkedAt));

        return account;
    }

    /// <summary>
    /// Links an external credential to this account. Idempotent per provider subject: re-presenting a
    /// subject that is already linked returns the existing row untouched — in particular its
    /// <see cref="ExternalLogin.Email"/> snapshot is never rewritten. A genuinely different provider
    /// account carries a different subject and so becomes its own row.
    /// </summary>
    public ExternalLogin LinkExternalLogin(
        ExternalLoginProvider provider, string subject, string? email, bool emailVerified, DateTimeOffset when)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var trimmed = subject.Trim();
        var active = _externalLogins.FirstOrDefault(
            login => login.IsActive && login.Provider == provider && login.Subject == trimmed);

        if (active is not null)
        {
            return active;
        }

        // A previously revoked link is left as history; re-linking creates a fresh row.
        var linked = ExternalLogin.Link(Id, provider, trimmed, email, emailVerified, when);
        _externalLogins.Add(linked);
        return linked;
    }

    /// <summary>
    /// Revokes an external credential. Always safe to do: the mobile number is a credential the account
    /// cannot be without, so unlinking every social login still leaves a way in.
    /// </summary>
    public void RevokeExternalLogin(ExternalLoginId externalLoginId, DateTimeOffset when)
    {
        var login = _externalLogins.FirstOrDefault(l => l.Id == externalLoginId && l.IsActive)
            ?? throw new DomainException("No active external login with that id on this account.");

        login.Revoke(when);
    }

    public void AddRole(Role role)
    {
        if (!_roles.Contains(role))
        {
            _roles.Add(role);
        }
    }

    public void RemoveRole(Role role)
    {
        if (_roles.Count == 1 && _roles.Contains(role))
        {
            throw new DomainException("A user must retain at least one role.");
        }

        _roles.Remove(role);
    }

    public void LinkToCustomer(CustomerId customerId) => CustomerId = customerId;


    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    /// <summary>
    /// Sets (or resets) the account's password from an already-hashed value. Clears any brute-force
    /// state — a fresh credential starts with a clean slate.
    /// </summary>
    public void SetPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        FailedLoginAttempts = 0;
        LockedOutUntil = null;
    }

    /// <summary>Whether a password lockout is currently in effect (PAS-002 §7).</summary>
    public bool IsLockedOut(DateTimeOffset now) => LockedOutUntil is { } until && now < until;

    /// <summary>
    /// Records a failed password attempt. On reaching <see cref="MaxFailedLoginAttempts"/> the account is
    /// locked for <see cref="LoginLockoutDuration"/> and the counter resets, so the next window starts clean.
    /// </summary>
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockedOutUntil = now.Add(LoginLockoutDuration);
            FailedLoginAttempts = 0;
        }
    }

    /// <summary>Clears brute-force state and stamps the successful sign-in.</summary>
    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginAttempts = 0;
        LockedOutUntil = null;
        RecordLogin(now);
    }

    public void Lock() => Deactivate(AccountStatus.Locked);

    public void Suspend() => Deactivate(AccountStatus.Suspended);

    public void Activate() => Status = AccountStatus.Active;

    /// <summary>Only an active account may sign in, refresh, or keep a session (GL-12).</summary>
    public bool CanSignIn => Status == AccountStatus.Active;

    private void Deactivate(AccountStatus status)
    {
        if (Status == status)
        {
            return;
        }

        var wasActive = CanSignIn;
        Status = status;
        if (wasActive)
        {
            RaiseDomainEvent(new UserAccountDeactivatedDomainEvent(Id, status));
        }
    }

    /// <summary>
    /// POPIA erasure (PAS-002): tombstone the login identity's personal data and suspend it. Mobile/email are
    /// unique, so the mobile becomes a per-account pseudonym and the email is cleared. Idempotent.
    /// </summary>
    public void Anonymize()
    {
        if (Status == AccountStatus.Suspended && FirstName == "[erased]")
        {
            return;
        }

        FirstName = "[erased]";
        LastName = "[erased]";
        MobileNumber = ("erased-" + Id.Value.ToString("N"))[..32];   // unique, fits the 32-char column
        Email = null;
        Deactivate(AccountStatus.Suspended);
    }

    private static string? Normalise(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
