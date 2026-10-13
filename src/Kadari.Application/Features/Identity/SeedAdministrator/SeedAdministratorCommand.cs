using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Features.Identity.SeedAdministrator;

/// <summary>
/// Ensures a <c>SystemAdministrator</c> account exists so the very first administrator can exist at
/// all — nothing else grants that role, and <c>AssignRole</c> itself requires it. Idempotent: safe on
/// every boot.
/// </summary>
/// <param name="MobileNumber">The sign-in identifier for the administrator. Required.</param>
/// <param name="Email">Overrides <see cref="AdministratorSeed.Email"/> when supplied.</param>
/// <param name="Password">Optional initial portal password; hashed and set (or rotated) when supplied.</param>
/// <param name="FirstName">Overrides <see cref="AdministratorSeed.FirstName"/> for a newly created account.</param>
/// <param name="LastName">Overrides <see cref="AdministratorSeed.LastName"/> for a newly created account.</param>
public sealed record SeedAdministratorCommand(
    string MobileNumber, string? Email = null, string? Password = null,
    string? FirstName = null, string? LastName = null) : ICommand<SeedAdministratorResponse>, IUnscopedRequest;

/// <param name="Created">A new account was registered.</param>
/// <param name="Promoted">An account already existed on that mobile and gained the role.</param>
public sealed record SeedAdministratorResponse(Guid UserAccountId, bool Created, bool Promoted);
