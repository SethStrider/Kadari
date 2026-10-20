using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Development.Services;
using Kadari.Domain.Access;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Identity;
using Kadari.Domain.Owners;

namespace Kadari.Application.Features.Development.SeedDemoData;

/// <summary>
/// Development only: the cast for a walkthrough of KADARI. An insurer (Sentinel Home Insurance) with a console user,
/// a customer-success user and a valuer who sign in to the portal with a password, and three customers who sign in to
/// the app with a code: two insured by Sentinel with households already captured, one consumer-direct with an empty
/// ledger to capture live. Run at start-up by the Development-only demo seeder.
/// <para>
/// Idempotent per person: anyone already on file (matched by mobile) is left exactly as they are, so re-running never
/// duplicates and never undoes what a demo changed.
/// </para>
/// </summary>
/// <param name="StaffPassword">The portal password every seeded staff user gets.</param>
// Unscoped: a start-up seeding command, not a request about a customer-owned resource.
public sealed record SeedDemoDataCommand(string StaffPassword) : ICommand<SeedDemoDataResponse>, IUnscopedRequest;

public sealed record SeedDemoDataResponse(int StaffCreated, int CustomersCreated, int AssetsCreated);

internal sealed class SeedDemoDataHandler(IKadariDbContext db, IPasswordHasher passwordHasher, TimeProvider clock)
    : ICommandHandler<SeedDemoDataCommand, SeedDemoDataResponse>
{
    public const string InsurerName = "Sentinel Home Insurance";

    private sealed record DemoStaff(string Mobile, string Email, string FirstName, string LastName, Role Role);

    private sealed record DemoCustomer(
        string Mobile, string FirstName, string LastName, bool InsuredBySentinel, DemoItem[] Household);

    private static readonly DemoStaff[] Staff =
    [
        new("+27825550201", "support@demo.kadari.co.za", "Lerato", "Mokoena", Role.CustomerSuccess),
        new("+27825550202", "valuer@demo.kadari.co.za", "David", "Botha", Role.Valuer),
        new("+27825550203", "insurer@demo.kadari.co.za", "Nadia", "Pillay", Role.Insurer),
    ];

    private static readonly DemoCustomer[] Customers =
    [
        new("+27825550101", "Thandi", "Nkosi", InsuredBySentinel: true, DemoHousehold.Family),
        new("+27825550102", "Johan", "van Wyk", InsuredBySentinel: true, DemoHousehold.Flat),
        new("+27825550103", "Aisha", "Patel", InsuredBySentinel: false, []),
    ];

    public async Task<SeedDemoDataResponse> Handle(SeedDemoDataCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var sentinel = await EnsureInsurerAsync(ct);

        var staffCreated = 0;
        foreach (var person in Staff)
        {
            if (await db.UserAccounts.AnyAsync(u => u.MobileNumber == person.Mobile, ct))
            {
                continue;
            }

            var user = UserAccount.Register(person.Mobile, person.FirstName, person.LastName, "ZA", person.Role, person.Email);
            user.SetPassword(passwordHasher.Hash(cmd.StaffPassword));
            db.UserAccounts.Add(user);
            if (person.Role == Role.Insurer)
            {
                db.InsurerUsers.Add(InsurerUser.Grant(user.Id, sentinel.Id, now));
            }

            staffCreated++;
        }

        var customersCreated = 0;
        var assetsCreated = 0;
        foreach (var person in Customers)
        {
            if (await db.UserAccounts.AnyAsync(u => u.MobileNumber == person.Mobile, ct))
            {
                continue;
            }

            // The Customer is created and linked here, so the sign-up reaction sees it and leaves the account alone.
            var customer = Customer.Register(person.FirstName, person.LastName, person.Mobile, "ZA");
            var user = UserAccount.Register(person.Mobile, person.FirstName, person.LastName, "ZA", Role.Customer);
            user.LinkToCustomer(customer.Id);
            db.Customers.Add(customer);
            db.UserAccounts.Add(user);

            // Capture needs image-processing consent; granting it up front keeps the demo on the product, not the form.
            db.KadariConsents.Add(KadariConsent.Grant(customer.Id, KadariConsentScope.AssetImageProcessing, now));

            var tenantId = person.InsuredBySentinel ? sentinel.Id : TenantId.Default;
            if (person.InsuredBySentinel)
            {
                db.TenantMemberships.Add(TenantMembership.Assign(customer.Id, sentinel.Id, now));
            }

            var owner = Owner.ForCustomer(customer.Id, $"{person.FirstName} {person.LastName}");
            db.Owners.Add(owner);
            assetsCreated += DemoHousehold.Register(db, owner.Id, tenantId, person.Household, now);
            customersCreated++;
        }

        await db.SaveChangesAsync(ct);
        return new SeedDemoDataResponse(staffCreated, customersCreated, assetsCreated);
    }

    private async Task<Tenant> EnsureInsurerAsync(CancellationToken ct)
    {
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Name == InsurerName, ct);
        if (existing is not null)
        {
            return existing;
        }

        var tenant = Tenant.CreateInsurer(InsurerName);
        tenant.Rebrand(new TenantBranding("#0B3D91", "#F2A900", logoReference: null));
        db.Tenants.Add(tenant);
        return tenant;
    }
}
