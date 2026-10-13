namespace Kadari.Domain.Subjects;

/// <summary>
/// What is being assessed — a person or a business (DES-001b). The canonical, first-class Subject Type the
/// whole platform shares: the Verification and OSINT engines both reference it (OSINT's older local
/// <c>EntityType</c> was merged into this).
/// </summary>
public enum SubjectType
{
    Person,
    Business,
}
