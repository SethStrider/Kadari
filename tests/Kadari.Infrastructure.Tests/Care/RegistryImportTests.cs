using Microsoft.EntityFrameworkCore;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.CareRegistry.ApplyRegistryImport;
using Kadari.Application.Features.CareRegistry.PreviewRegistryImport;
using Kadari.Application.Features.CareRegistry.Services;
using Kadari.Domain.CareRegistry;
using Kadari.Infrastructure.CareRegistry;
using static Kadari.Infrastructure.Tests.Care.CareTestHost;

namespace Kadari.Infrastructure.Tests.Care;

/// <summary>
/// The registry import (CARE/14 C2, CARE/10) against real Postgres: the founder's real workbook files import cleanly,
/// re-importing is a no-op, what is applied is exactly what was previewed, and an unpublishable rule blocks the
/// whole import.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistryImportTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private static string RepositoryFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kadari.sln")))
        {
            dir = dir.Parent;
        }

        return Path.Combine([dir!.FullName, .. parts]);
    }

    private static Task<RegistrySnapshot> WorkbookAsync() => RegistryFiles.LoadAsync(new RegistryFilesOptions
    {
        ResearchDirectory = RepositoryFile("docs", "authority", "CARE", "REGISTRY"),
        CoverageRulesFile = RepositoryFile("docs", "care", "registry", "coverage-rules.csv"),
    }, CancellationToken.None);

    [Fact]
    public async Task TheFoundersWorkbook_ImportsEveryBrandRetailerAndRouteWithItsSource()
    {
        var snapshot = await WorkbookAsync();

        var result = await ImportAsync(ConnectionString, snapshot);

        await using var db = NewContext(ConnectionString);
        Assert.Equal(
            (64, 30, 10, true),
            (await db.Set<Brand>().CountAsync(), await db.Set<Retailer>().CountAsync(), await db.Set<RoutingModel>().CountAsync(),
             await db.Set<Brand>().AllAsync(b => b.SourceEvidenceId != null)));
        Assert.True(result.Added > 0);
    }

    [Fact]
    public async Task Reimporting_TheSameFiles_ChangesNothing()
    {
        var snapshot = await WorkbookAsync();
        await ImportAsync(ConnectionString, snapshot);

        await using var db = NewContext(ConnectionString);
        var plan = await Sender(db).Send(new PreviewRegistryImportQuery(snapshot));

        Assert.All(plan.Changes, c => Assert.Equal(RegistryAction.Unchanged, c.Action));
    }

    [Fact]
    public async Task Apply_WithAFingerprintThatIsNotTheCurrentPlan_IsRefusedAndAppliesNothing()
    {
        var snapshot = Snapshot(BoschRule());

        await using (var db = NewContext(ConnectionString))
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                Sender(db).Send(new ApplyRegistryImportCommand(snapshot, "not-the-previewed-plan")));
        }

        await using var verify = NewContext(ConnectionString);
        Assert.Equal(0, await verify.Set<Brand>().CountAsync());
    }

    [Fact]
    public async Task ARuleMarkedPublishedWithoutADuration_BlocksTheWholeImport()
    {
        var snapshot = Snapshot(BoschRule(months: ""));

        await using (var db = NewContext(ConnectionString))
        {
            var sender = Sender(db);
            var plan = await sender.Send(new PreviewRegistryImportQuery(snapshot));
            Assert.False(plan.CanApply);
            await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new ApplyRegistryImportCommand(snapshot, plan.Fingerprint)));
        }

        await using var verify = NewContext(ConnectionString);
        Assert.Equal(0, await verify.Set<Brand>().CountAsync());
    }

    [Fact]
    public async Task ARuleForABrandOutsideTheRegistry_IsAnError()
    {
        var rule = BoschRule() with { Brand = "Unknown Marketplace Brand" };

        await using var db = NewContext(ConnectionString);
        var plan = await Sender(db).Send(new PreviewRegistryImportQuery(Snapshot(rule)));

        Assert.Contains(plan.Errors, e => e.Contains("Unknown Marketplace Brand"));
    }

    [Fact]
    public async Task AnEditedRule_BecomesTheNextVersionAndSupersedesTheLiveOne()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule(months: "24")));

        await ImportAsync(ConnectionString, Snapshot(BoschRule(months: "36")));

        await using var db = NewContext(ConnectionString);
        var versions = await db.Set<CoverageRuleVersion>().OrderBy(v => v.Version)
            .Select(v => new { v.Version, v.Status, v.DurationMonths }).ToListAsync();
        Assert.Equal(
            [new { Version = 1, Status = RuleStatus.Superseded, DurationMonths = (int?)24 },
             new { Version = 2, Status = RuleStatus.Published, DurationMonths = (int?)36 }],
            versions);
    }
}
