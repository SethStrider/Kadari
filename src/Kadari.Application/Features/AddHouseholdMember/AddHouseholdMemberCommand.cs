using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Subjects.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.AddHouseholdMember;

/// <summary>
/// Share one of a customer's assets with a household member (deck §31): add the member as a co-owner, so the
/// item shows in their ledger too without being duplicated. Customer-scoped — only the primary owner manages
/// their asset's household. Idempotent (re-adding is a no-op).
/// </summary>
public sealed record AddHouseholdMemberCommand(Guid CustomerId, Guid AssetId, Guid MemberCustomerId)
    : ICommand<HouseholdMemberResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record HouseholdMemberResponse(Guid AssetId, Guid MemberCustomerId, int CoOwnerCount);

internal sealed class AddHouseholdMemberHandler(IKadariDbContext db, SubjectRegistrar subjects, TimeProvider clock)
    : ICommandHandler<AddHouseholdMemberCommand, HouseholdMemberResponse>
{
    public async Task<HouseholdMemberResponse> Handle(AddHouseholdMemberCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);
        var memberId = new CustomerId(cmd.MemberCustomerId);
        var now = clock.GetUtcNow();

        if (!await db.Set<Customer>().AnyAsync(c => c.Id == memberId, ct))
        {
            throw new NotFoundException(nameof(Customer), cmd.MemberCustomerId);
        }

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

        var memberSubject = await subjects.GetOrCreateForCustomerAsync(memberId, ct);
        asset.AddCoOwner(memberSubject, now);

        await db.SaveChangesAsync(ct);

        return new HouseholdMemberResponse(cmd.AssetId, cmd.MemberCustomerId, asset.CoOwners.Count);
    }
}
