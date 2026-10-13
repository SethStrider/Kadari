using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Subjects.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.SettleEstate;

/// <summary>
/// Settle a deceased customer's estate (deck §31): pass every one of their assets to an heir as inheritance.
/// Each asset (and its passport) is re-pointed to the heir and raises the distinct inheritance event.
/// Administrative and cross-customer — gated by <c>Permission.AssetEstateSettle</c>, so an
/// <see cref="IUnscopedRequest"/>. Idempotent-ish: re-running after all assets have moved inherits nothing.
/// </summary>
public sealed record SettleEstateCommand(Guid DeceasedCustomerId, Guid HeirCustomerId)
    : ICommand<SettleEstateResponse>, IUnscopedRequest;

public sealed record SettleEstateResponse(int AssetsInherited);

internal sealed class SettleEstateHandler(IKadariDbContext db, SubjectRegistrar subjects, TimeProvider clock)
    : ICommandHandler<SettleEstateCommand, SettleEstateResponse>
{
    public async Task<SettleEstateResponse> Handle(SettleEstateCommand cmd, CancellationToken ct)
    {
        var deceasedId = new CustomerId(cmd.DeceasedCustomerId);
        var heirId = new CustomerId(cmd.HeirCustomerId);
        var now = clock.GetUtcNow();

        if (!await db.Set<Customer>().AnyAsync(c => c.Id == deceasedId, ct))
        {
            throw new NotFoundException(nameof(Customer), cmd.DeceasedCustomerId);
        }

        if (!await db.Set<Customer>().AnyAsync(c => c.Id == heirId, ct))
        {
            throw new NotFoundException(nameof(Customer), cmd.HeirCustomerId);
        }

        var deceasedSubjectId = await db.Subjects.AsNoTracking()
            .Where(s => s.CustomerId == deceasedId)
            .Select(s => (SubjectId?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (deceasedSubjectId is null)
        {
            return new SettleEstateResponse(0);   // the deceased captured nothing — nothing to inherit
        }

        var heirSubject = await subjects.GetOrCreateForCustomerAsync(heirId, ct);

        var assets = await db.Assets.Where(a => a.OwnerId == deceasedSubjectId.Value).ToListAsync(ct);
        foreach (var asset in assets)
        {
            asset.Inherit(heirSubject, now);
        }

        var passports = await db.AssetPassports.Where(p => p.OwnerId == deceasedSubjectId.Value).ToListAsync(ct);
        foreach (var passport in passports)
        {
            passport.ReassignOwner(heirSubject, now);
        }

        await db.SaveChangesAsync(ct);

        return new SettleEstateResponse(assets.Count);
    }
}
