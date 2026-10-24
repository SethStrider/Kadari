using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Features.CareRegistry.Services;

namespace Kadari.Application.Features.CareRegistry.PreviewRegistryImport;

/// <summary>
/// Show what importing a registry snapshot would change, without changing anything (CARE/10, CARE/14 C2): every
/// source, brand, retailer, routing model and coverage rule as add / update / new version / unchanged, plus every
/// row that would block the import. Apply it with <c>ApplyRegistryImportCommand</c> and the plan's fingerprint.
/// </summary>
/// <remarks>Unscoped: the registry is platform reference data with no customer in it; the migration runner and
/// KADARI staff run it.</remarks>
public sealed record PreviewRegistryImportQuery(RegistrySnapshot Snapshot) : IQuery<RegistryImportPlan>, IUnscopedRequest;

internal sealed class PreviewRegistryImportHandler(RegistryImporter importer)
    : IQueryHandler<PreviewRegistryImportQuery, RegistryImportPlan>
{
    public Task<RegistryImportPlan> Handle(PreviewRegistryImportQuery query, CancellationToken ct) =>
        importer.PlanAsync(query.Snapshot, ct);
}
