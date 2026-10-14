namespace Kadari.Domain.Owners;

/// <summary>
/// What is being assessed — a person or a business (DES-001b). The canonical, first-class Owner Type the
/// whole platform shares: the Verification and OSINT engines both reference it (OSINT's older local
/// <c>EntityType</c> was merged into this).
/// </summary>
public enum OwnerType
{
    Person,
    Business,
}
