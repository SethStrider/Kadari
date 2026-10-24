using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Kadari.Application;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.CareRegistry.ApplyRegistryImport;
using Kadari.Application.Features.CareRegistry.PreviewRegistryImport;
using Kadari.Application.Features.CareRegistry.Services;
using Kadari.Application.Features.Identity.SeedAdministrator;
using Kadari.Application.Features.SeedDefaultTenant;
using Kadari.Infrastructure.CareRegistry;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;
using Kadari.Infrastructure.Persistence.Maintenance;
using Kadari.Infrastructure.Persistence.Seeding;
using Kadari.Infrastructure.Security;

// ---------------------------------------------------------------------------------------------------
// Kadari.Migration — one-shot database migrate + seed runner.
//
//   Local:    the Aspire AppHost runs this before the WebApi starts (tables + seeds guaranteed).
//   Pipeline: run on deploy with ConnectionStrings__kadaridb set; a non-zero exit gates the release.
//
//   Arguments:  --migrate-only       apply migrations, skip seeding
//               --seed-only          seed only (database must already be migrated)
//               --reencrypt-fields   after the rest, move every encrypted column onto the current
//                                    FieldEncryption key (run once after a key rotation, GL-24)
//               --registry-preview   show what the Care registry import would change, and apply nothing
//               --skip-registry      seed without importing the Care registry
//
//   The Care registry (docs/authority/CARE/REGISTRY + docs/care/registry) is imported on every seeding run:
//   previewed, then the same plan applied. A plan with errors fails the run; an unchanged registry is a no-op.
// ---------------------------------------------------------------------------------------------------

var builder = Host.CreateApplicationBuilder(args);

// The runner registers the whole Application layer but only the persistence it needs, so handlers it never sends
// (capture, proofs) have unresolvable dependencies such as blob storage. Development's build-time validation of every
// registration would refuse to start on those; scope validation stays on.
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
{
    ValidateOnBuild = false,
    ValidateScopes = true,
}));

var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);
var seedOnly = args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase);
var reencryptFields = args.Contains("--reencrypt-fields", StringComparer.OrdinalIgnoreCase);
var registryPreview = args.Contains("--registry-preview", StringComparer.OrdinalIgnoreCase);
var skipRegistry = args.Contains("--skip-registry", StringComparer.OrdinalIgnoreCase);

// The same field-encryption key ring the API uses (GL-24), so anything this job writes or re-encrypts
// reads back in them.
var fieldKeys = FieldProtector.ConfigureFrom(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("kadaridb")
    ?? throw new InvalidOperationException(
        "Missing connection string 'kadaridb'. The Aspire AppHost injects it locally; " +
        "in a pipeline set the ConnectionStrings__kadaridb environment variable.");

// Only the persistence surface + the CQRS pipeline for the idempotent seed commands — no blob storage, no web
// host. The interceptor keeps seeded rows on the audit conventions.
builder.Services.AddDbContext<KadariDbContext>(options => options
    .UseNpgsql(connectionString)
    .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System)));
builder.Services.AddScoped<IKadariDbContext>(sp => sp.GetRequiredService<KadariDbContext>());
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddSingleton(TimeProvider.System);
// The administrator seed hashes an initial portal password, so the seed command's handler needs the
// hasher. We don't call AddInfrastructure() here (no blob storage in the one-shot runner), so register the
// hasher explicitly — same registration Infrastructure uses.
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddApplication();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Kadari.Migration");

try
{
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KadariDbContext>();

    await WaitForDatabaseAsync(db, logger);

    if (!seedOnly)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database is up to date — no pending migrations.");
        }
        else
        {
            logger.LogInformation("Applying {Count} migration(s): {Names}", pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrations applied.");
        }
    }

    if (!migrateOnly)
    {
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        // The consumer-direct default tenant every customer without an insurer belongs to (D11). Idempotent.
        var tenant = await sender.Send(new SeedDefaultTenantCommand());
        logger.LogInformation("Default tenant seed complete: created={Created}.", tenant.Created);

        // The first SystemAdministrator: nothing else can grant that role, so without it the portal is unreachable.
        var adminSeed = builder.Configuration.GetSection(AdministratorSeedOptions.SectionName)
            .Get<AdministratorSeedOptions>() ?? new AdministratorSeedOptions();

        if (string.IsNullOrWhiteSpace(adminSeed.MobileNumber))
        {
            logger.LogWarning(
                "No administrator seeded: {Section}:MobileNumber is not configured.",
                AdministratorSeedOptions.SectionName);
        }
        else
        {
            var admin = await sender.Send(new SeedAdministratorCommand(adminSeed.MobileNumber, adminSeed.Email));
            logger.LogInformation(
                "Administrator seed complete for account {AccountId}: created={Created}, promoted={Promoted}.",
                admin.UserAccountId, admin.Created, admin.Promoted);
        }
    }

    if (!migrateOnly && !skipRegistry)
    {
        var registryOptions = builder.Configuration.GetSection(RegistryFilesOptions.SectionName).Get<RegistryFilesOptions>()
            ?? new RegistryFilesOptions();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        if (!await ImportCareRegistryAsync(sender, registryOptions, registryPreview, logger))
        {
            return 1;
        }
    }

    if (reencryptFields)
    {
        var columns = await new FieldReencryptor(db, fieldKeys, logger).ReencryptAsync(CancellationToken.None);
        var unreadable = columns.Sum(c => c.Unreadable);
        logger.LogInformation(
            "Field re-encryption complete: {Count} value(s) across {Columns} column(s) now under key {KeyId}.",
            columns.Sum(c => c.Reencrypted), columns.Count, fieldKeys.CurrentKeyId);
        if (unreadable > 0)
        {
            // Fail the job: a value no key can read means a missing previous key, and retiring keys now would
            // lose it for good.
            logger.LogError("{Count} encrypted value(s) could not be read with any configured key.", unreadable);
            return 1;
        }
    }

    logger.LogInformation("Kadari.Migration finished successfully.");
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Kadari.Migration failed.");
    return 1;
}

// Care registry: preview the import, log the plan, and apply exactly that plan (unless previewing only). False = fail.
static async Task<bool> ImportCareRegistryAsync(ISender sender, RegistryFilesOptions options, bool previewOnly, ILogger logger)
{
    var snapshot = await RegistryFiles.LoadAsync(options, CancellationToken.None);
    var plan = await sender.Send(new PreviewRegistryImportQuery(snapshot));

    logger.LogInformation(
        "Care registry plan: {Add} to add, {Update} to update, {Versions} new rule versions, {Unchanged} unchanged (researched rules in the files: {Rules} coverage, {Registrations} registration, {Routes} routing).",
        plan.Count(RegistryAction.Add), plan.Count(RegistryAction.Update), plan.Count(RegistryAction.NewVersion),
        plan.Count(RegistryAction.Unchanged), snapshot.CoverageRules.Count, snapshot.RegistrationRules?.Count ?? 0,
        snapshot.RoutingRules?.Count ?? 0);

    foreach (var change in plan.Changes.Where(c => c.Action != RegistryAction.Unchanged))
    {
        logger.LogDebug("  {Action} {Entity} {Key}: {Detail}", change.Action, change.Entity, change.Key, change.Detail);
    }

    if (!plan.CanApply)
    {
        foreach (var error in plan.Errors)
        {
            logger.LogError("Care registry: {Error}", error);
        }

        return false;
    }

    if (previewOnly)
    {
        logger.LogInformation("Care registry preview only — nothing applied (plan {Fingerprint}).", plan.Fingerprint[..12]);
        return true;
    }

    if (plan.Changes.All(c => c.Action == RegistryAction.Unchanged))
    {
        logger.LogInformation("Care registry is up to date.");
        return true;
    }

    var result = await sender.Send(new ApplyRegistryImportCommand(snapshot, plan.Fingerprint));
    logger.LogInformation(
        "Care registry imported: {Added} added, {Updated} updated, {Versions} new rule versions.",
        result.Added, result.Updated, result.NewVersions);
    return true;
}

// The database container may still be starting (local spin-up) — poll before giving up.
static async Task WaitForDatabaseAsync(KadariDbContext db, ILogger logger)
{
    const int maxAttempts = 30;
    for (var attempt = 1; ; attempt++)
    {
        if (await db.Database.CanConnectAsync())
        {
            return;
        }

        if (attempt >= maxAttempts)
        {
            throw new TimeoutException($"Database was not reachable after {maxAttempts} attempts.");
        }

        logger.LogInformation("Database not ready (attempt {Attempt}/{Max}) — retrying in 2s…", attempt, maxAttempts);
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
