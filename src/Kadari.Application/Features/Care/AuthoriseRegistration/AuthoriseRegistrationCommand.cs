using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.GetRegistration;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Care.AuthoriseRegistration;

/// <summary>
/// The owner authorises KADARI to register their product for extra cover (CARE/05 C04, CARE/14 C5). Requires explicit
/// consent to the current wording; records the authorisation with exactly what will be shared; starts the registration
/// in the rule's mode (Guided or Assisted — Automatic runs as Assisted in V1) and audits it. Idempotent: the same
/// <see cref="IdempotencyKey"/> returns the same registration, and the key is unique in the database, so a retried
/// request can never start a second registration. Customer-scoped.
/// </summary>
public sealed record AuthoriseRegistrationCommand(
    Guid CustomerId,
    Guid AssetId,
    Guid OpportunityId,
    string AcceptedTermsVersion,
    bool ExplicitConsent,
    string IdempotencyKey) : ICommand<RegistrationDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class AuthoriseRegistrationHandler(
    IKadariDbContext db, OwnedAssets assets, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<AuthoriseRegistrationCommand, RegistrationDto>
{
    public async Task<RegistrationDto> Handle(AuthoriseRegistrationCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.CustomerId, cmd.AssetId, ct);
        var key = cmd.IdempotencyKey.Trim();

        // A replay of the same request: answer with what it already started.
        var replay = await db.Set<CoverageRegistration>().AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == key, ct);
        if (replay is not null)
        {
            return replay.AssetId == asset.Id
                ? await DescribeAsync(replay, ct)
                : throw new ConflictException("This idempotency key was already used for another request.");
        }

        if (cmd.AcceptedTermsVersion != RegistrationPack.TermsVersion)
        {
            throw new ConflictException("The authorisation wording has changed. Read it again before agreeing.");
        }

        if (user.AccountId is not { } accountId)
        {
            throw new ForbiddenException("Only the signed-in owner can authorise a registration.");
        }

        var opportunity = await db.Set<RegistrationOpportunity>()
            .FirstOrDefaultAsync(o => o.Id == new RegistrationOpportunityId(cmd.OpportunityId) && o.AssetId == asset.Id, ct)
            ?? throw new NotFoundException(nameof(RegistrationOpportunity), cmd.OpportunityId);
        if (!opportunity.IsOpen)
        {
            throw new ConflictException($"This opportunity is {opportunity.Status} and can no longer be taken.");
        }

        var rule = await db.Set<RegistrationRuleVersion>().AsNoTracking().FirstAsync(r => r.Id == opportunity.RuleVersionId, ct);
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == new CustomerId(cmd.CustomerId), ct);
        var shared = RegistrationPack.For(rule, customer, asset);
        var now = clock.GetUtcNow();

        var authorisation = ExternalActionAuthorisation.Grant(
            accountId, asset.Id, ExternalActionType.WarrantyRegistration,
            JsonSerializer.Serialize(new AuthorisedScope(rule.Destination, shared), ScopeJson.Options),
            RegistrationPack.TermsVersion, now);
        db.Set<ExternalActionAuthorisation>().Add(authorisation);

        var registration = CoverageRegistration.Start(opportunity, rule, authorisation, key, now);
        db.Set<CoverageRegistration>().Add(registration);

        db.Set<ExternalActionAudit>().Add(ExternalActionAudit.Record(
            asset.Id, ExternalActionType.WarrantyRegistration, authorisation.Id, ExternalActor.Owner, accountId,
            rule.Destination, "Authorised", now,
            sharedSummary: string.Join(", ", shared.Select(s => s.Label)), result: registration.Mode.ToString()));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IdempotencyKey", StringComparison.Ordinal) == true)
        {
            // A concurrent request with the same key won the race; this one must not start a second registration.
            throw new ConflictException("This registration is already being started. Refresh to see it.");
        }

        return ToDto(registration, rule, shared);
    }

    private async Task<RegistrationDto> DescribeAsync(CoverageRegistration registration, CancellationToken ct)
    {
        var rule = await db.Set<RegistrationRuleVersion>().AsNoTracking().FirstAsync(r => r.Id == registration.RuleVersionId, ct);
        var scope = await db.Set<ExternalActionAuthorisation>().AsNoTracking()
            .Where(a => a.Id == registration.AuthorisationId).Select(a => a.ScopeJson).FirstAsync(ct);
        return ToDto(registration, rule, GetRegistrationHandler.SharedFrom(scope));
    }

    private static RegistrationDto ToDto(CoverageRegistration r, RegistrationRuleVersion rule, IReadOnlyList<SharedFieldDto> shared) =>
        new(r.Id.Value, r.Status.ToString(), r.Mode.ToString(), rule.DestinationType.ToString(), r.Destination, rule.Instructions,
            r.StartedAt, r.ExternalReference, r.RegisteredOn, r.FailureReason, shared);
}
