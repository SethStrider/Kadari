using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.VerifyAssetPassport;

/// <summary>
/// Public, no-login verification of an Asset Passport by its <c>AP-</c> id — the QR / secure-link target an owner
/// hands to an insurer, assessor or buyer (spec D9). Unscoped because the verifier has no account: the 128-bit
/// id is the capability, shared deliberately by the owner. Discloses the credential (asset, confidence, value,
/// live status) but never the owner. Unknown id → 404; a revoked passport still answers, as not valid.
/// </summary>
public sealed record VerifyAssetPassportQuery(string VerificationId)
    : IQuery<AssetPassportVerificationDto>, IUnscopedRequest;

public sealed record AssetPassportVerificationDto(
    bool Valid,
    string VerificationId,
    string Status,
    string DisplayName,
    string Category,
    string Assurance,
    long? ReplacementValueMinor,
    string? Currency,
    int EvidenceCount,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastUpdatedAt);

internal sealed class VerifyAssetPassportHandler(IKadariDbContext db)
    : IQueryHandler<VerifyAssetPassportQuery, AssetPassportVerificationDto>
{
    public async Task<AssetPassportVerificationDto> Handle(VerifyAssetPassportQuery query, CancellationToken ct)
    {
        var p = await db.AssetPassports.AsNoTracking()
            .FirstOrDefaultAsync(x => x.VerificationId == query.VerificationId, ct)
            ?? throw new NotFoundException(nameof(AssetPassport), query.VerificationId);

        return new AssetPassportVerificationDto(
            p.IsValid, p.VerificationId, p.Status.ToString(), p.DisplayName, p.Category.ToString(),
            p.Assurance.ToString(), p.ReplacementValue?.AmountMinor, p.ReplacementValue?.Currency,
            p.EvidenceCount, p.IssuedAt, p.LastUpdatedAt);
    }
}
