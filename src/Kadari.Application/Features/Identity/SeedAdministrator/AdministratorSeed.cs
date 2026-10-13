namespace Kadari.Application.Features.Identity.SeedAdministrator;

/// <summary>
/// The shipped identity of the platform's first administrator. Only the mobile number is deployment
/// specific (it is the sign-in credential — PCS-004 §10 makes mobile the primary identifier), so it is
/// supplied per environment rather than baked in here.
/// </summary>
public static class AdministratorSeed
{
    public const string Email = "admin@trusti.co.za";
    public const string FirstName = "System";
    public const string LastName = "Administrator";
    public const string Country = "ZA";
}
