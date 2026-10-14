using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Identity;
using Kadari.Domain.Owners;

namespace Kadari.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for KADARI (PostgreSQL, schema <c>kadari</c>). Entity configurations are auto-discovered;
/// every <see cref="Entity"/> gets a global soft-delete filter + xmin concurrency. Domain events are collected via
/// <see cref="DequeueDomainEvents"/> and dispatched inside the command's transaction by a pipeline behaviour.
/// </summary>
public class KadariDbContext(DbContextOptions<KadariDbContext> options)
    : DbContext(options), IKadariDbContext
{
    public const string DefaultSchema = "kadari";

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Owner> Owners => Set<Owner>();

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AssetPassport> AssetPassports => Set<AssetPassport>();
    public DbSet<KadariConsent> KadariConsents => Set<KadariConsent>();
    public DbSet<VisionUsage> VisionUsages => Set<VisionUsage>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<InsurerUser> InsurerUsers => Set<InsurerUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KadariDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplySoftDeleteQueryFilters();
        modelBuilder.ApplyXminConcurrency();
    }

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var aggregates = ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        return domainEvents;
    }
}
