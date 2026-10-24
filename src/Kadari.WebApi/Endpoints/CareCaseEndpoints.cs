using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kadari.Application.Features.Care.AcceptCaseRoute;
using Kadari.Application.Features.Care.AddCaseCommitment;
using Kadari.Application.Features.Care.AddCaseReference;
using Kadari.Application.Features.Care.BookCaseAppointment;
using Kadari.Application.Features.Care.GetCase;
using Kadari.Application.Features.Care.GetCases;
using Kadari.Application.Features.Care.KeepCaseCommitment;
using Kadari.Application.Features.Care.MarkCaseSent;
using Kadari.Application.Features.Care.RecordProviderUpdate;
using Kadari.Application.Features.Care.ReportIssue;
using Kadari.Application.Features.Care.ResolveCase;
using Kadari.Application.Features.Care.Services;
using Kadari.Application.Features.Care.SettleCaseAppointment;
using Kadari.Application.Features.Care.WithdrawCase;
using Kadari.Domain.Access;
using Kadari.WebApi.Access;
using Kadari.WebApi.Http;

namespace Kadari.WebApi.Endpoints;

/// <summary>
/// "Something's wrong" and the cases it opens (CARE/13, CARE/14 C7–C11): report a problem on an existing asset, see
/// where it goes, send the case pack through the provider's own channel, then record references, replies, promises,
/// appointments and the outcome. Customer self-service behind the <c>Care</c> launch switch; ownership is enforced by
/// the pipeline. Enum values are names (e.g. "Fault").
/// </summary>
public static class CareCaseEndpoints
{
    public static IEndpointRouteBuilder MapCareCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kadari/customers/{customerId:guid}/assets/{assetId:guid}/care/cases")
            .WithTags("Care cases")
            .RequireFeature(features => features.Care);

        group.MapPost("/", async (Guid customerId, Guid assetId, ReportIssueRequest body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new ReportIssueCommand(customerId, assetId, body.IssueClass, body.Description, body.OwnerSaysUnsafe), ct);
                return Results.Created($"/api/v1/kadari/customers/{customerId}/assets/{assetId}/care/cases/{result.CaseId}", result);
            })
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>(StatusCodes.Status201Created)
            .WithName("ReportIssue")
            .WithSummary("Something's wrong: open a case. The safety gate runs first; the route comes from the registry, or a person finds it.");

        group.MapGet("/", async (Guid customerId, Guid assetId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCasesQuery(customerId, assetId), ct)))
            .RequirePermission(Permission.CareView)
            .Produces<IReadOnlyList<CaseSummaryDto>>()
            .WithName("GetCases")
            .WithSummary("The asset's cases, open first.");

        group.MapGet("/{caseId:guid}", async (Guid customerId, Guid assetId, Guid caseId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCaseQuery(customerId, assetId, caseId), ct)))
            .RequirePermission(Permission.CareView)
            .Produces<CaseDto>()
            .WithName("GetCase")
            .WithSummary("One case: timeline, route, references, promises, appointments, messages and the case pack to send.");

        group.MapPost("/{caseId:guid}/route/accept", async (Guid customerId, Guid assetId, Guid caseId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new AcceptCaseRouteCommand(customerId, assetId, caseId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("AcceptCaseRoute")
            .WithSummary("Confirm a likely (amber) route before sending.");

        group.MapPost("/{caseId:guid}/sent", async (
                Guid customerId, Guid assetId, Guid caseId, MarkCaseSentRequest body,
                [FromHeader(Name = IdempotencyKeyFilter.HeaderName)] string idempotencyKey, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new MarkCaseSentCommand(
                    customerId, assetId, caseId, body.AcceptedTermsVersion, body.ExplicitConsent, idempotencyKey), ct)))
            .RequirePermission(Permission.CareManage)
            .AddEndpointFilter<IdempotencyKeyFilter>()
            .Produces<CaseDto>()
            .WithName("MarkCaseSent")
            .WithSummary("Record that the owner sent the case pack through the provider's channel. Needs agreement to the current wording and an Idempotency-Key; a replay records nothing new.");

        group.MapPost("/{caseId:guid}/references", async (
                Guid customerId, Guid assetId, Guid caseId, CaseReferenceRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new AddCaseReferenceCommand(customerId, assetId, caseId, body.Party, body.Reference), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("AddCaseReference")
            .WithSummary("Add a reference number the provider gave.");

        group.MapPost("/{caseId:guid}/updates", async (
                Guid customerId, Guid assetId, Guid caseId, ProviderUpdateRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new RecordProviderUpdateCommand(customerId, assetId, caseId, body.Status, body.Note), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("RecordProviderUpdate")
            .WithSummary("Capture the provider's reply or new status (e.g. \"WaitingPart\").");

        group.MapPost("/{caseId:guid}/commitments", async (
                Guid customerId, Guid assetId, Guid caseId, CaseCommitmentRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new AddCaseCommitmentCommand(
                    customerId, assetId, caseId, body.Party, body.Description, body.DueOn), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("AddCaseCommitment")
            .WithSummary("Record a promise with a date. A provider promise still open the day after is chased.");

        group.MapPost("/{caseId:guid}/commitments/{commitmentId:guid}/kept", async (
                Guid customerId, Guid assetId, Guid caseId, Guid commitmentId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new KeepCaseCommitmentCommand(customerId, assetId, caseId, commitmentId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("KeepCaseCommitment")
            .WithSummary("Mark a promise kept.");

        group.MapPost("/{caseId:guid}/appointments", async (
                Guid customerId, Guid assetId, Guid caseId, CaseAppointmentRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new BookCaseAppointmentCommand(
                    customerId, assetId, caseId, body.StartsAt, body.EndsAt, body.Location, body.Instructions), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("BookCaseAppointment")
            .WithSummary("Record a booked visit, collection or drop-off.");

        group.MapPost("/{caseId:guid}/appointments/{appointmentId:guid}/settle", async (
                Guid customerId, Guid assetId, Guid caseId, Guid appointmentId, SettleAppointmentRequest body, ISender sender,
                CancellationToken ct) =>
                Results.Ok(await sender.Send(new SettleCaseAppointmentCommand(customerId, assetId, caseId, appointmentId, body.Done), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("SettleCaseAppointment")
            .WithSummary("An appointment happened (done) or was cancelled.");

        group.MapPost("/{caseId:guid}/resolve", async (
                Guid customerId, Guid assetId, Guid caseId, ResolveCaseRequest body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ResolveCaseCommand(customerId, assetId, caseId, body.Outcome, body.Notes), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("ResolveCase")
            .WithSummary("Close the case with its outcome; the outcome is written to the asset's history.");

        group.MapPost("/{caseId:guid}/withdraw", async (Guid customerId, Guid assetId, Guid caseId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new WithdrawCaseCommand(customerId, assetId, caseId), ct)))
            .RequirePermission(Permission.CareManage)
            .Produces<CaseDto>()
            .WithName("WithdrawCase")
            .WithSummary("Close a case the owner no longer needs.");

        return group;
    }

    /// <summary>Body of the report-issue endpoint: the kind of problem by name (Fault, AccidentalDamage, DeadOnArrival),
    /// what's wrong, and the owner's own answer to "is it unsafe?".</summary>
    public sealed record ReportIssueRequest(string IssueClass, string Description, bool? OwnerSaysUnsafe);

    /// <summary>Body of the mark-sent endpoint: the wording version the owner read, and their agreement.</summary>
    public sealed record MarkCaseSentRequest(string AcceptedTermsVersion, bool ExplicitConsent);

    public sealed record CaseReferenceRequest(string Party, string Reference);

    public sealed record ProviderUpdateRequest(string Status, string? Note);

    /// <summary>Party by name: Provider or Owner.</summary>
    public sealed record CaseCommitmentRequest(string Party, string Description, DateOnly DueOn);

    public sealed record CaseAppointmentRequest(DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Location, string? Instructions);

    public sealed record SettleAppointmentRequest(bool Done);

    /// <summary>Outcome by name: Repaired, Replaced, Refunded, Credited, NoFaultFound, Rejected, ResolvedOther.</summary>
    public sealed record ResolveCaseRequest(string Outcome, string? Notes);
}
