using Kadari.Domain.Abstractions;
using Kadari.Domain.Subjects;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when a stolen asset is recovered and returns to active ownership (deck §30).</summary>
public sealed record AssetRecoveredDomainEvent(AssetId AssetId, SubjectId OwnerId, DateTimeOffset RecoveredAt)
    : IDomainEvent;
