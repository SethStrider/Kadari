using Kadari.Domain.Abstractions;
using Kadari.Domain.Owners;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when an asset passes to an heir on estate settlement (deck §31) — a transfer whose cause is
/// inheritance, kept distinct for audit and downstream handling.</summary>
public sealed record AssetInheritedDomainEvent(AssetId AssetId, OwnerId From, OwnerId To, DateTimeOffset At)
    : IDomainEvent;
