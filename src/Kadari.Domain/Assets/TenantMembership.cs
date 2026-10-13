using Kadari.Domain.Abstractions;
using Kadari.Domain.Customers;

namespace Kadari.Domain.Assets;

public readonly record struct TenantMembershipId(Guid Value)
{
    public static TenantMembershipId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// Which tenant partition a customer's Kadari data belongs to (D2/D11). Kept as a small Kadari-side
/// mapping rather than a field on the core <c>Customer</c>: tenancy is an Kadari need only, and 0.3's rule
/// is not to retrofit the core with it. A customer with no membership is implicitly on
/// <see cref="TenantId.Default"/> (the consumer-direct platform tenant); assigning a cohort to an insurer
/// creates or re-points this. One membership per customer.
/// </summary>
public sealed class TenantMembership : AggregateRoot<TenantMembershipId>
{
    private TenantMembership() { }   // EF

    private TenantMembership(TenantMembershipId id, CustomerId customerId, TenantId tenantId, DateTimeOffset at)
        : base(id)
    {
        CustomerId = customerId;
        TenantId = tenantId;
        AssignedAt = at;
    }

    public CustomerId CustomerId { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    public static TenantMembership Assign(CustomerId customerId, TenantId tenantId, DateTimeOffset at) =>
        new(TenantMembershipId.New(), customerId, tenantId, at);

    /// <summary>Move the customer to a different tenant (idempotent on an unchanged tenant).</summary>
    public void Reassign(TenantId tenantId, DateTimeOffset at)
    {
        TenantId = tenantId;
        AssignedAt = at;
    }
}
