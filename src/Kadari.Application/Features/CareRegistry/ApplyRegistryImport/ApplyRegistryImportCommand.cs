using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.CareRegistry.Services;
using Kadari.Domain.Audit;

namespace Kadari.Application.Features.CareRegistry.ApplyRegistryImport;

/// <summary>
/// Apply a previewed registry import (CARE/10, CARE/14 C2). The plan is recomputed inside the transaction and must
/// match <see cref="ExpectedFingerprint"/> — the fingerprint of the preview — so exactly what was reviewed is what
/// lands; if the registry moved in between, nothing is applied (409). A plan with errors is refused (400). The
/// import is audited.
/// </summary>
/// <remarks>Unscoped: the registry is platform reference data with no customer in it; the migration runner and
/// KADARI staff run it.</remarks>
public sealed record ApplyRegistryImportCommand(RegistrySnapshot Snapshot, string ExpectedFingerprint)
    : ICommand<RegistryImportResult>, IUnscopedRequest;

public sealed record RegistryImportResult(int Added, int Updated, int NewVersions, int Unchanged, string Fingerprint);

internal sealed class ApplyRegistryImportHandler(IKadariDbContext db, RegistryImporter importer, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ApplyRegistryImportCommand, RegistryImportResult>
{
    public async Task<RegistryImportResult> Handle(ApplyRegistryImportCommand cmd, CancellationToken ct)
    {
        var plan = await importer.PlanAsync(cmd.Snapshot, ct);

        if (!plan.CanApply)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["registry"] = [.. plan.Errors] });
        }

        if (!string.Equals(plan.Fingerprint, cmd.ExpectedFingerprint, StringComparison.Ordinal))
        {
            throw new ConflictException("The registry changed since the import was previewed. Preview it again.");
        }

        await importer.ApplyAsync(cmd.Snapshot, ct);

        var result = new RegistryImportResult(
            plan.Count(RegistryAction.Add), plan.Count(RegistryAction.Update), plan.Count(RegistryAction.NewVersion),
            plan.Count(RegistryAction.Unchanged), plan.Fingerprint);

        db.Set<AuditEvent>().Add(AuditEvent.Record(
            user.IsAuthenticated ? ActorKind.Account : ActorKind.System, "CareRegistry", Guid.Empty, "RegistryImported",
            clock.GetUtcNow(), actorId: user.AccountId,
            newState: $"added={result.Added} updated={result.Updated} versions={result.NewVersions} fingerprint={plan.Fingerprint[..12]}"));

        await db.SaveChangesAsync(ct);
        return result;
    }
}
