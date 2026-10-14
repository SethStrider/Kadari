using Kadari.Domain.Abstractions;

namespace Kadari.Domain.Customers;

public enum CustomerStatus
{
    Active,
    Suspended,
    Closed,
}

/// <summary>
/// An individual using KADARI (CDM-001). The anchor most other entities hang off. Created through
/// <see cref="Register"/>, which enforces the required-field invariants; state changes go through
/// methods, never public setters (CLAUDE.md §5).
/// </summary>
public sealed class Customer : AggregateRoot<CustomerId>
{
    private Customer() { }   // EF

    private Customer(
        CustomerId id, string firstName, string lastName, string mobileNumber,
        string country, string? email, string? identityNumber, DateOnly? dateOfBirth)
        : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        MobileNumber = mobileNumber;
        Country = country;
        Email = email;
        IdentityNumber = identityNumber;
        DateOfBirth = dateOfBirth;
        Status = CustomerStatus.Active;

        RaiseDomainEvent(new CustomerRegisteredDomainEvent(id));
    }

    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string MobileNumber { get; private set; } = null!;
    public string Country { get; private set; } = null!;   // ISO 3166-1 alpha-2
    public string? Email { get; private set; }
    public string? IdentityNumber { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public CustomerStatus Status { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static Customer Register(
        string firstName, string lastName, string mobileNumber, string country,
        string? email = null, string? identityNumber = null, DateOnly? dateOfBirth = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        return new Customer(
            CustomerId.New(), firstName.Trim(), lastName.Trim(), mobileNumber.Trim(),
            country.Trim().ToUpperInvariant(), email?.Trim(), identityNumber?.Trim(), dateOfBirth);
    }

    /// <summary>
    /// Mobile-first provisioning (PCS-004): create the anchor Customer from just a verified mobile, mirroring
    /// <see cref="Kadari.Domain.Identity.UserAccount.ProvisionForMobile"/>. Name and profile are captured
    /// later (CaptureCustomerIdentity), so the names start empty. Used by the auto-register-on-first-OTP
    /// reaction, so a signed-in customer immediately has the record every scoped slice — and <c>/me</c> —
    /// resolves through.
    /// </summary>
    public static Customer ProvisionForMobile(string mobileNumber, string country = "ZA")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        return new Customer(
            CustomerId.New(), string.Empty, string.Empty, mobileNumber.Trim(),
            country.Trim().ToUpperInvariant(), email: null, identityNumber: null, dateOfBirth: null);
    }

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    /// <summary>
    /// Capture the customer's identity inputs (national ID number, optionally date of birth) — supplied when
    /// they submit their ID for verification. Trimmed; the latest submission is authoritative. This is the
    /// value the identity rail verifies and, once verified, the basis of the subject's durable identity key.
    /// </summary>
    public void RecordIdentity(string identityNumber, DateOnly? dateOfBirth = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityNumber);
        if (Status == CustomerStatus.Closed)
        {
            throw new DomainException("An erased customer cannot record identity.");
        }

        IdentityNumber = identityNumber.Trim();
        if (dateOfBirth is not null)
        {
            DateOfBirth = dateOfBirth;
        }
    }

    public void Suspend() => Status = CustomerStatus.Suspended;

    /// <summary>
    /// POPIA erasure / right to be forgotten (PAS-002): tombstone the customer's personal data in place.
    /// The record and its id survive — so the immutable audit trail and referential integrity hold (DB
    /// standards: nothing hard-deleted) — but the person is no longer identifiable. Idempotent.
    /// </summary>
    public void Anonymize()
    {
        if (Status == CustomerStatus.Closed)
        {
            return;   // already erased
        }

        const string tombstone = "[erased]";
        FirstName = tombstone;
        LastName = tombstone;
        MobileNumber = tombstone;   // the mobile index is non-unique, so a shared tombstone is safe
        Email = null;
        IdentityNumber = null;
        DateOfBirth = null;
        LastLoginAt = null;
        Status = CustomerStatus.Closed;

        RaiseDomainEvent(new CustomerErasedDomainEvent(Id));
    }
}
