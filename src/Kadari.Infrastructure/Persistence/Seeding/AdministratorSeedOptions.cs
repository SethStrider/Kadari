using Kadari.Application.Features.Identity.SeedAdministrator;

namespace Kadari.Infrastructure.Persistence.Seeding;

/// <summary>
/// Who the platform's first administrator is. Bound from the <c>AdministratorSeed</c> configuration
/// section; the mobile number has no default on purpose — it is a sign-in credential, and a shipped
/// default would be a well-known one anybody could request an OTP against.
/// </summary>
public sealed class AdministratorSeedOptions
{
    public const string SectionName = "AdministratorSeed";

    /// <summary>The administrator's mobile in E.164 (e.g. <c>+27820000000</c>). Seeding is skipped while blank.</summary>
    public string MobileNumber { get; set; } = string.Empty;

    public string Email { get; set; } = AdministratorSeed.Email;

    /// <summary>
    /// Optional initial portal password. When set, the seed hashes it and sets it on the administrator
    /// account (re-seeding rotates it), enabling password sign-in to portal.trustli.co.za. Left blank in
    /// production — set it once out-of-band, or leave the admin on OTP-only. Never commit a real value.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Additional administrators to seed alongside the primary one above. Each is created (or promoted)
    /// to <c>SystemAdministrator</c> and given its password, idempotently by mobile number. Used to seed
    /// the known staff team into a fresh database so the portal has real logins from the first boot.
    /// Dev-only in committed config; production supplies its own out-of-band.
    /// </summary>
    public List<AdministratorSeedEntry> Administrators { get; set; } = [];
}

/// <summary>One additional administrator to seed. Mobile is the identity key (unique); email is the
/// portal sign-in; the name is cosmetic (falls back to the shipped defaults when blank).</summary>
public sealed class AdministratorSeedEntry
{
    public string MobileNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
