using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care.Events;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

public readonly record struct CoverageRegistrationId(Guid Value)
{
    public static CoverageRegistrationId New() => new(Guid.CreateVersion7());
}

/// <summary>Where a registration is (CARE/12 §10 RegistrationStatus, CARE/07 §6).</summary>
public enum RegistrationStatus
{
    WaitingUserStep,
    Completed,
    FailedRetryable,
    FailedManual,
    Expired,
    Cancelled,
}

/// <summary>
/// One attempt to activate extra cover (CARE/12 §2 CoverageRegistration). It starts only from an explicit,
/// recorded authorisation, carries an idempotency key that is unique in the database (a retried request finds the
/// same registration instead of making a second), follows the exact rule version that offered it, and is
/// <see cref="RegistrationStatus.Completed"/> only with proof — an external reference from the provider — never
/// because a form was opened (CARE/07 §6, CARE/14 C5 hard rules).
/// </summary>
public sealed class CoverageRegistration : AggregateRoot<CoverageRegistrationId>
{
    public const int MaxReferenceLength = 120;
    public const int MaxReasonLength = 500;

    private CoverageRegistration() { }   // EF

    private CoverageRegistration(CoverageRegistrationId id) : base(id) { }

    public AssetId AssetId { get; private init; }
    public RegistrationOpportunityId OpportunityId { get; private init; }
    public RegistrationRuleVersionId RuleVersionId { get; private init; }
    public ExternalActionAuthorisationId AuthorisationId { get; private init; }

    /// <summary>The rule's mode as executed. Automatic is not executed in V1 and runs as Assisted.</summary>
    public RegistrationMode Mode { get; private init; }

    public string Destination { get; private init; } = null!;
    public string IdempotencyKey { get; private init; } = null!;
    public RegistrationStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private init; }
    public string? ExternalReference { get; private set; }
    public DateOnly? RegisteredOn { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }

    public bool IsOpen => Status is RegistrationStatus.WaitingUserStep or RegistrationStatus.FailedRetryable;

    /// <summary>
    /// Start a registration the owner has authorised. Takes the opportunity (so it is not offered again) and waits for
    /// the owner's step on the official channel.
    /// </summary>
    public static CoverageRegistration Start(
        RegistrationOpportunity opportunity, RegistrationRuleVersion rule, ExternalActionAuthorisation authorisation,
        string idempotencyKey, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(authorisation);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (opportunity.RuleVersionId != rule.Id)
        {
            throw new DomainException("A registration follows the rule version its opportunity offered.");
        }

        if (!authorisation.Covers(ExternalActionType.WarrantyRegistration, opportunity.AssetId, at))
        {
            throw new DomainException("A registration needs the owner's current authority for this asset.");
        }

        opportunity.Take(at);

        var registration = new CoverageRegistration(CoverageRegistrationId.New())
        {
            AssetId = opportunity.AssetId,
            OpportunityId = opportunity.Id,
            RuleVersionId = rule.Id,
            AuthorisationId = authorisation.Id,
            Mode = rule.Mode == RegistrationMode.Automatic ? RegistrationMode.Assisted : rule.Mode,
            Destination = rule.Destination,
            IdempotencyKey = idempotencyKey.Trim(),
            Status = RegistrationStatus.WaitingUserStep,
            StartedAt = at,
        };
        registration.RaiseDomainEvent(new WarrantyRegistrationStartedDomainEvent(registration.Id, registration.AssetId, at));
        return registration;
    }

    /// <summary>The provider confirmed the registration. The external reference is the proof; without it, it isn't done.</summary>
    public void Complete(string externalReference, DateOnly registeredOn, DateTimeOffset at)
    {
        if (!IsOpen)
        {
            throw new DomainException($"A {Status} registration cannot be completed.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        var reference = externalReference.Trim();
        if (reference.Length > MaxReferenceLength)
        {
            throw new DomainException($"A reference is at most {MaxReferenceLength} characters.");
        }

        if (registeredOn > DateOnly.FromDateTime(at.UtcDateTime))
        {
            throw new DomainException("A registration cannot be confirmed for a future date.");
        }

        ExternalReference = reference;
        RegisteredOn = registeredOn;
        CompletedAt = at;
        Status = RegistrationStatus.Completed;
        RaiseDomainEvent(new WarrantyRegistrationCompletedDomainEvent(Id, AssetId, reference, registeredOn, at));
    }

    /// <summary>
    /// The attempt failed. A retryable failure (the provider's site was down) keeps it open; anything else needs a
    /// person. External failure is not KADARI failure: the registration and its authority are kept either way.
    /// </summary>
    public void Fail(string reason, bool retryable, DateTimeOffset at)
    {
        if (!IsOpen)
        {
            throw new DomainException($"A {Status} registration cannot fail.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmed = reason.Trim();
        FailureReason = trimmed.Length > MaxReasonLength ? trimmed[..MaxReasonLength] : trimmed;
        FailedAt = at;
        Status = retryable ? RegistrationStatus.FailedRetryable : RegistrationStatus.FailedManual;
        RaiseDomainEvent(new WarrantyRegistrationFailedDomainEvent(Id, AssetId, Status, FailureReason, at));
    }

    /// <summary>The owner changed their mind before completing.</summary>
    public void Cancel(DateTimeOffset at)
    {
        if (!IsOpen)
        {
            throw new DomainException($"A {Status} registration cannot be cancelled.");
        }

        Status = RegistrationStatus.Cancelled;
        FailedAt = at;
    }

    /// <summary>The deadline passed without a confirmation.</summary>
    public void Expire(DateTimeOffset at)
    {
        if (!IsOpen)
        {
            return;
        }

        Status = RegistrationStatus.Expired;
        FailedAt = at;
    }
}
