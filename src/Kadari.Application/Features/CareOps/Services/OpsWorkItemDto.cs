using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.Services;

public sealed record OpsWorkItemDto(
    Guid WorkItemId,
    string WorkType,
    string Severity,
    string Status,
    string ReasonCode,
    string Summary,
    Guid? AssetId,
    Guid? CaseId,
    Guid? AssignedTo,
    DateTimeOffset RaisedAt,
    DateTimeOffset? DueAt,
    bool Overdue,
    string? Resolution,
    string? ResolutionNotes,
    DateTimeOffset? ResolvedAt)
{
    internal static OpsWorkItemDto From(OpsWorkItem w, DateTimeOffset now) => new(
        w.Id.Value, w.WorkType.ToString(), w.Severity.ToString(), w.Status.ToString(), w.ReasonCode, w.Summary, w.AssetId?.Value,
        w.CaseId?.Value, w.AssignedTo, w.RaisedAt, w.DueAt, w.Status != OpsWorkStatus.Resolved && w.DueAt < now,
        w.Resolution?.ToString(), w.ResolutionNotes, w.ResolvedAt);
}
