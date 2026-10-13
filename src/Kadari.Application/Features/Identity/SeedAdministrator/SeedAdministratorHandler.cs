using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Identity;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Access;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Identity.SeedAdministrator;

internal sealed class SeedAdministratorHandler(IKadariDbContext db, IPasswordHasher passwordHasher)
    : ICommandHandler<SeedAdministratorCommand, SeedAdministratorResponse>
{
    /// <summary>
    /// The roles the bootstrap administrator holds. It administers the platform (SystemAdministrator) and,
    /// so the portal's Trust Journeys configuration works out of the box, the catalogue/journey templates
    /// too (ProductAdministrator — the PF-001 draft/publish flow is gated on ProductCreate/ProductPublish).
    /// Roles are additive; a Customer role the account already carries survives.
    /// </summary>
    private static readonly Role[] SeededRoles = [Role.SystemAdministrator, Role.ProductAdministrator];

    public async Task<SeedAdministratorResponse> Handle(SeedAdministratorCommand command, CancellationToken ct)
    {
        var mobile = command.MobileNumber.Trim();
        var email = string.IsNullOrWhiteSpace(command.Email) ? AdministratorSeed.Email : command.Email.Trim();

        // Mobile — not email — decides identity: it is the primary identifier and carries the unique
        // index. The account may already exist because the administrator signed in before this ran
        // (VerifyOtp auto-registers an unknown mobile as a Customer), so promote rather than collide.
        var existing = await db.Set<UserAccount>().FirstOrDefaultAsync(u => u.MobileNumber == mobile, ct);

        if (existing is null)
        {
            var firstName = string.IsNullOrWhiteSpace(command.FirstName) ? AdministratorSeed.FirstName : command.FirstName.Trim();
            var lastName = string.IsNullOrWhiteSpace(command.LastName) ? AdministratorSeed.LastName : command.LastName.Trim();

            var user = UserAccount.Register(
                mobile, firstName, lastName,
                AdministratorSeed.Country, Role.SystemAdministrator, email);
            AddMissingRoles(user);   // ProductAdministrator alongside the SystemAdministrator it registered with

            ApplyPassword(user, command.Password);
            db.Set<UserAccount>().Add(user);
            await db.SaveChangesAsync(ct);

            return new SeedAdministratorResponse(user.Id.Value, Created: true, Promoted: false);
        }

        // Even when the account already exists and already holds the roles, a supplied password is still
        // applied — this is the sanctioned path to (re)set the first administrator's portal password.
        ApplyPassword(existing, command.Password);

        // Promote it to hold every seeded role (additive — a Customer role from a prior OTP sign-in survives).
        var promoted = AddMissingRoles(existing);
        await db.SaveChangesAsync(ct);

        return new SeedAdministratorResponse(existing.Id.Value, Created: false, Promoted: promoted);
    }

    /// <summary>Adds any <see cref="SeededRoles"/> the account is missing. Returns whether it changed.</summary>
    private static bool AddMissingRoles(UserAccount user)
    {
        var changed = false;
        foreach (var role in SeededRoles)
        {
            if (!user.Roles.Contains(role))
            {
                user.AddRole(role);
                changed = true;
            }
        }

        return changed;
    }

    private void ApplyPassword(UserAccount user, string? password)
    {
        if (!string.IsNullOrWhiteSpace(password))
        {
            user.SetPassword(passwordHasher.Hash(password));
        }
    }
}
