using Microsoft.EntityFrameworkCore;
using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.Customers;
using Kadari.Domain.Identity;
using Kadari.Domain.Owners;

namespace Kadari.Application.Abstractions.Persistence;

/// <summary>
/// The persistence surface the Application layer depends on: entity sets plus <c>SaveChangesAsync</c>, and a hook to
/// drain domain events for dispatch. Implemented by <c>KadariDbContext</c> in Infrastructure.
/// </summary>
public interface IKadariDbContext
{
    // Customers and the owners their assets belong to.
    DbSet<Customer> Customers { get; }
    DbSet<Owner> Owners { get; }

    // Sign-in.
    DbSet<UserAccount> UserAccounts { get; }
    DbSet<Session> Sessions { get; }
    DbSet<OtpChallenge> OtpChallenges { get; }

    // The asset ledger, passports, consent, insurer tenants.
    DbSet<Asset> Assets { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<AssetPassport> AssetPassports { get; }
    DbSet<KadariConsent> KadariConsents { get; }
    DbSet<VisionUsage> VisionUsages { get; }
    DbSet<TenantMembership> TenantMemberships { get; }
    DbSet<InsurerUser> InsurerUsers { get; }

    // The asset's permanent history, and KADARI Care on the asset. Registry rows are reached through Set<T>().
    DbSet<AssetHistoryEntry> AssetHistory { get; }
    DbSet<CareProfile> CareProfiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    /// <summary>Collect and clear the domain events raised on tracked aggregates, for dispatch inside the transaction.</summary>
    IReadOnlyList<IDomainEvent> DequeueDomainEvents();
}
