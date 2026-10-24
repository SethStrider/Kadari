using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Care.GetRegistration;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.MarkCaseSent;

/// <summary>
/// The owner sent the case pack through the provider's own channel (CARE/14 C10). They saw exactly what would leave
/// KADARI and agreed to the current wording; the handoff is recorded as an authorisation (what was shared, with whom)
/// and an audit row, and the case moves to Sent. Idempotent: the same <see cref="IdempotencyKey"/> returns the case as
/// it is, and the key is unique per case in the database, so a retried request never records a second handoff.
/// Customer-scoped.
/// </summary>
public sealed record MarkCaseSentCommand(
    Guid CustomerId,
    Guid AssetId,
    Guid CaseId,
    string AcceptedTermsVersion,
    bool ExplicitConsent,
    string IdempotencyKey) : ICommand<CaseDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class MarkCaseSentHandler(IKadariDbContext db, CaseCommands cases, ICurrentUser user)
    : ICommandHandler<MarkCaseSentCommand, CaseDto>
{
    public async Task<CaseDto> Handle(MarkCaseSentCommand cmd, CancellationToken ct)
    {
        var (asset, @case) = await cases.LoadAsync(cmd.CustomerId, cmd.AssetId, cmd.CaseId, ct);
        var key = cmd.IdempotencyKey.Trim();
        if (@case.Messages.Any(m => m.IdempotencyKey == key))
        {
            return CaseView.Detail(@case, asset, DateOnly.FromDateTime(cases.Now.UtcDateTime));
        }

        if (cmd.AcceptedTermsVersion != CasePack.TermsVersion)
        {
            throw new ConflictException("The wording has changed. Read it again before sending.");
        }

        if (user.AccountId is not { } accountId)
        {
            throw new ForbiddenException("Only the signed-in owner can send a case.");
        }

        if (@case.NeedsRoute || @case.IsClosed)
        {
            throw new ConflictException("This case cannot be sent yet.");
        }

        var pack = CasePack.For(asset, @case);
        var destination = @case.ChannelValue ?? @case.TargetName ?? @case.RouteType.ToString();
        var channel = @case.ChannelType?.ToString() ?? "Other";
        var now = cases.Now;

        var authorisation = ExternalActionAuthorisation.Grant(
            accountId, asset.Id, ExternalActionType.CaseHandoff,
            JsonSerializer.Serialize(new AuthorisedScope(destination, pack.Shared), ScopeJson.Options), CasePack.TermsVersion, now);
        db.Set<ExternalActionAuthorisation>().Add(authorisation);

        var sharedSummary = string.Join(", ", pack.Shared.Where(s => s.Value is not null).Select(s => s.Label));
        @case.MarkSent(channel, sharedSummary, key, now);

        db.Set<ExternalActionAudit>().Add(ExternalActionAudit.Record(
            asset.Id, ExternalActionType.CaseHandoff, authorisation.Id, ExternalActor.Owner, accountId, destination, "Sent", now,
            sharedSummary: sharedSummary, result: channel, caseId: @case.Id.Value));

        try
        {
            return await cases.SaveAsync(asset, @case, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IdempotencyKey", StringComparison.Ordinal) == true)
        {
            // A concurrent request with the same key won the race; it recorded the handoff.
            throw new ConflictException("This case is already being sent. Refresh to see it.");
        }
    }
}
