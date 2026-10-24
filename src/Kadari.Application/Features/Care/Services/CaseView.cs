using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

public sealed record CaseSummaryDto(
    Guid CaseId,
    string Title,
    string Status,
    string StatusLabel,
    string RouteType,
    string RouteConfidence,
    bool NeedsRoute,
    bool SafetyFlagged,
    DateTimeOffset OpenedAt,
    DateOnly? NextActionOn,
    bool HasOverduePromise,
    string? Outcome);

public sealed record CaseEventDto(string Type, string Actor, string Summary, DateTimeOffset OccurredAt);

public sealed record CaseReferenceDto(Guid ReferenceId, string Party, string Reference, DateTimeOffset AddedAt);

public sealed record CaseCommitmentDto(Guid CommitmentId, string Party, string Description, DateOnly DueOn, string Status, bool Overdue);

public sealed record CaseAppointmentDto(
    Guid AppointmentId, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Location, string? Instructions, string Status);

public sealed record CaseMessageDto(string Direction, string Channel, string Body, DateTimeOffset At);

public sealed record CaseRouteDto(
    string RouteType,
    string Label,
    string Confidence,
    bool Accepted,
    bool NeedsRoute,
    string? TargetName,
    string? ChannelType,
    string? ChannelValue,
    string? Instructions,
    IReadOnlyList<string> RequiredEvidence);

public sealed record CaseDto(
    Guid CaseId,
    Guid AssetId,
    string Title,
    string IssueSummary,
    string IssueClass,
    string CaseType,
    string Status,
    string StatusLabel,
    bool SafetyFlagged,
    string? SafetyGuidance,
    string CoverageAtOpen,
    CaseRouteDto Route,
    DateTimeOffset OpenedAt,
    DateOnly? NextActionOn,
    DateTimeOffset? ClosedAt,
    string? Outcome,
    string? OutcomeNotes,
    IReadOnlyList<CaseEventDto> Timeline,
    IReadOnlyList<CaseReferenceDto> References,
    IReadOnlyList<CaseCommitmentDto> Commitments,
    IReadOnlyList<CaseAppointmentDto> Appointments,
    IReadOnlyList<CaseMessageDto> Messages,
    CasePackDto? Pack);

/// <summary>Loads an asset's cases (owner-checked through the asset first) and shapes them for the owner.</summary>
internal static class CaseView
{
    public static async Task<AssetCase> GetAsync(IKadariDbContext db, Asset asset, Guid caseId, bool tracked, CancellationToken ct)
    {
        var query = db.Set<AssetCase>()
            .Include(c => c.References)
            .Include(c => c.Commitments)
            .Include(c => c.Appointments);
        return await (tracked ? query : query.AsNoTracking())
            .FirstOrDefaultAsync(c => c.Id == new AssetCaseId(caseId) && c.AssetId == asset.Id, ct)
            ?? throw new NotFoundException(nameof(AssetCase), caseId);
    }

    public static CaseSummaryDto Summary(AssetCase c, DateOnly today) => new(
        c.Id.Value, c.Title, c.Status.ToString(), AssetCase.Describe(c.Status), c.RouteType.ToString(), c.RouteConfidence.ToString(),
        c.NeedsRoute, c.SafetyFlagged, c.OpenedAt, c.NextActionOn, c.Commitments.Any(x => x.IsOverdueOn(today)), c.Outcome?.ToString());

    public static CaseDto Detail(AssetCase c, Asset asset, DateOnly today) => new(
        c.Id.Value, c.AssetId.Value, c.Title, c.IssueSummary, c.IssueClass.ToString(), c.CaseType.ToString(), c.Status.ToString(),
        AssetCase.Describe(c.Status), c.SafetyFlagged, c.SafetyFlagged && !c.IsClosed ? SafetyGate.Guidance : null,
        c.CoverageAtOpen.ToString(),
        new CaseRouteDto(c.RouteType.ToString(), AssetCase.Describe(c.RouteType), c.RouteConfidence.ToString(), c.RouteAccepted,
            c.NeedsRoute, c.TargetName, c.ChannelType?.ToString(), c.ChannelValue, c.Instructions,
            c.RequiredEvidence.Select(e => e.ToString()).ToList()),
        c.OpenedAt, c.NextActionOn, c.ClosedAt, c.Outcome?.ToString(), c.OutcomeNotes,
        c.Events.OrderBy(e => e.Sequence)
            .Select(e => new CaseEventDto(e.Type.ToString(), e.Actor.ToString(), e.Summary, e.OccurredAt)).ToList(),
        c.References.OrderBy(r => r.AddedAt).Select(r => new CaseReferenceDto(r.Id.Value, r.Party, r.Reference, r.AddedAt)).ToList(),
        c.Commitments.OrderBy(x => x.DueOn)
            .Select(x => new CaseCommitmentDto(x.Id.Value, x.Party.ToString(), x.Description, x.DueOn, x.Status.ToString(), x.IsOverdueOn(today)))
            .ToList(),
        c.Appointments.OrderBy(a => a.StartsAt)
            .Select(a => new CaseAppointmentDto(a.Id.Value, a.StartsAt, a.EndsAt, a.Location, a.Instructions, a.Status.ToString())).ToList(),
        c.Messages.OrderBy(m => m.At).Select(m => new CaseMessageDto(m.Direction.ToString(), m.Channel, m.Body, m.At)).ToList(),
        c.IsClosed || c.NeedsRoute ? null : CasePack.For(asset, c));
}
