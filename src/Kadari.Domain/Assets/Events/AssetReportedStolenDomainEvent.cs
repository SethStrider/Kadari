using Kadari.Domain.Abstractions;
using Kadari.Domain.Subjects;

namespace Kadari.Domain.Assets.Events;

/// <summary>Raised when an owner reports an asset stolen (feeds AssetCheck, deck §30).</summary>
public sealed record AssetReportedStolenDomainEvent(AssetId AssetId, SubjectId OwnerId, DateTimeOffset ReportedAt)
    : IDomainEvent;
