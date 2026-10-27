using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;

namespace Kadari.Domain.Care.Events;

/// <summary>The owner authorised a registration and it started (CARE/12 §11 WarrantyRegistrationAuthorised).</summary>
public sealed record WarrantyRegistrationStartedDomainEvent(
    CoverageRegistrationId RegistrationId, AssetId AssetId, DateTimeOffset At) : IDomainEvent;

/// <summary>The provider confirmed the registration (CARE/12 §11 WarrantyRegistrationCompleted).</summary>
public sealed record WarrantyRegistrationCompletedDomainEvent(
    CoverageRegistrationId RegistrationId, AssetId AssetId, string ExternalReference, DateOnly RegisteredOn,
    DateTimeOffset At) : IDomainEvent;

/// <summary>A registration attempt failed (CARE/12 §11 WarrantyRegistrationFailed).</summary>
public sealed record WarrantyRegistrationFailedDomainEvent(
    CoverageRegistrationId RegistrationId, AssetId AssetId, RegistrationStatus Status, string Reason, DateTimeOffset At)
    : IDomainEvent;
