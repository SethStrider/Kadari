using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AddHouseholdMember;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.RemoveHouseholdMember;

/// <summary>
/// Remove a household member from one of a customer's assets (deck §31) — they stop co-owning it, so it leaves
/// their ledger. Customer-scoped — only the primary owner manages their asset's household. Idempotent: removing
/// someone who is not a co-owner is a no-op.
/// </summary>
public sealed record RemoveHouseholdMemberCommand(Guid CustomerId, Guid AssetId, Guid MemberCustomerId)
    : ICommand<HouseholdMemberResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class RemoveHouseholdMemberHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<RemoveHouseholdMemberCommand, HouseholdMemberResponse>
{
    public async Task<HouseholdMemberResponse> Handle(RemoveHouseholdMemberCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);
        var memberId = new CustomerId(cmd.MemberCustomerId);

        var ownerSubjectId = await db.Subjects.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (SubjectId?)s.Id)
            .FirstOrDefaultAsync(ct);

        var assetId = new AssetId(cmd.AssetId);
        var asset = ownerSubjectId is null
            ? null
            : await db.Assets
                .Include(a => a.CoOwners)
                .FirstOrDefaultAsync(a => a.Id == assetId && a.OwnerId == ownerSubjectId.Value, ct);

        if (asset is null)
        {
            throw new NotFoundException(nameof(Asset), cmd.AssetId);
        }

        var memberSubjectId = await db.Subjects.AsNoTracking()
            .Where(s => s.CustomerId == memberId)
            .Select(s => (SubjectId?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (memberSubjectId is not null)
        {
            asset.RemoveCoOwner(memberSubjectId.Value, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }

        return new HouseholdMemberResponse(cmd.AssetId, cmd.MemberCustomerId, asset.CoOwners.Count);
    }
}
