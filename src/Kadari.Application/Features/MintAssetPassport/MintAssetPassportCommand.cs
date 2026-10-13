using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.GetAssetPassport;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.MintAssetPassport;

/// <summary>
/// Mint (or refresh) the Asset Passport for one of a customer's assets (deck §16, spec D9). Customer-scoped —
/// a caller may only mint a passport for an asset in their own ledger. Idempotent: a second call refreshes
/// the existing passport's snapshot from the asset's current evidence rather than minting a duplicate, so one
/// asset only ever has one passport (and one stable <c>VerificationId</c>).
/// </summary>
public sealed record MintAssetPassportCommand(Guid CustomerId, Guid AssetId)
    : ICommand<AssetPassportDto>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class MintAssetPassportHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<MintAssetPassportCommand, AssetPassportDto>
{
    public async Task<AssetPassportDto> Handle(MintAssetPassportCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);

        var subjectId = await db.Subjects.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (SubjectId?)s.Id)
            .FirstOrDefaultAsync(ct);

        var assetId = new AssetId(cmd.AssetId);
        var asset = subjectId is null
            ? null
            : await db.Assets
                .Include(a => a.Evidence)
                .FirstOrDefaultAsync(a => a.Id == assetId && a.OwnerId == subjectId.Value, ct);

        if (asset is null)
        {
            throw new NotFoundException(nameof(Asset), cmd.AssetId);
        }

        var now = clock.GetUtcNow();
        var passport = await db.AssetPassports.FirstOrDefaultAsync(p => p.AssetId == assetId, ct);
        if (passport is null)
        {
            passport = AssetPassport.Mint(asset, $"AP-{Token()}", now);
            db.AssetPassports.Add(passport);
        }
        else
        {
            passport.Refresh(asset, now);
        }

        await db.SaveChangesAsync(ct);

        return AssetPassportDto.From(passport);
    }

    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
