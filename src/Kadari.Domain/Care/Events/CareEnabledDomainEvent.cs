using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care.Events;

/// <summary>Raised when Care is enabled on an asset (CARE/12 §11 CareProfileCreated).</summary>
public sealed record CareEnabledDomainEvent(
    CareProfileId CareProfileId, AssetId AssetId, CareCategory Category, DateTimeOffset At) : IDomainEvent;
