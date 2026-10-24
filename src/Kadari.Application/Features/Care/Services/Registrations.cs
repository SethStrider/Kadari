using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

/// <summary>A registration as the owner sees it (CARE/05 C04–C06).</summary>
public sealed record RegistrationDto(
    Guid RegistrationId,
    string Status,
    string Mode,
    string DestinationType,
    string Destination,
    string? Instructions,
    DateTimeOffset StartedAt,
    string? ExternalReference,
    DateOnly? RegisteredOn,
    string? FailureReason,
    IReadOnlyList<SharedFieldDto> Shared);

/// <summary>Loads an asset's registration for the registration slices (owner-checked through the asset first).</summary>
internal static class Registrations
{
    public static async Task<CoverageRegistration> GetAsync(
        IKadariDbContext db, Asset asset, Guid registrationId, CancellationToken ct) =>
        await db.Set<CoverageRegistration>()
            .FirstOrDefaultAsync(r => r.Id == new CoverageRegistrationId(registrationId) && r.AssetId == asset.Id, ct)
        ?? throw new NotFoundException(nameof(CoverageRegistration), registrationId);
}
