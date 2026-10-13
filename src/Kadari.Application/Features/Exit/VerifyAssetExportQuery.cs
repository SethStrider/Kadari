using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Reports;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Exit;

/// <summary>
/// Public, no-login verification of an asset-for-sale export token (spec §7) — what PoziSnap (or anyone) calls
/// to confirm the "Trustli-verified provenance" before trusting a listing. Two checks: the token's signature
/// must be genuine (only Trustli could mint it), and the referenced Asset Passport must still be live (a
/// revoked/suspended passport fails, even with a valid signature). No login, so <see cref="IUnscopedRequest"/>.
/// </summary>
public sealed record VerifyAssetExportQuery(string Token) : IQuery<AssetExportVerificationDto>, IUnscopedRequest;

public sealed record AssetExportVerificationDto(bool Valid, string? Reason, AssetForSalePackage? Package);

internal sealed class VerifyAssetExportHandler(IKadariDbContext db, IReportIntegritySigner signer)
    : IQueryHandler<VerifyAssetExportQuery, AssetExportVerificationDto>
{
    public async Task<AssetExportVerificationDto> Handle(VerifyAssetExportQuery query, CancellationToken ct)
    {
        if (!AssetExportProtocol.TryDecode(query.Token, signer, out var package) || package is null)
        {
            return new AssetExportVerificationDto(false, "The export signature is not genuine.", null);
        }

        // Live check: the credential must still be valid, not just have been signed once.
        var passport = await db.AssetPassports.AsNoTracking()
            .FirstOrDefaultAsync(p => p.VerificationId == package.PassportVerificationId, ct);

        if (passport is null)
        {
            return new AssetExportVerificationDto(false, "The referenced passport no longer exists.", package);
        }

        return passport.IsValid
            ? new AssetExportVerificationDto(true, null, package)
            : new AssetExportVerificationDto(false, $"The passport is {passport.Status}.", package);
    }
}
