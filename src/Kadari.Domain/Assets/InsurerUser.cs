using Kadari.Domain.Abstractions;
using Kadari.Domain.Identity;

namespace Kadari.Domain.Assets;

public readonly record struct InsurerUserId(Guid Value)
{
    public static InsurerUserId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// Binds a user account to the insurer tenant whose console it may use (Kadari §27–28) — the identity side
/// of tenancy, kept as an Kadari-side mapping rather than a field on the core <c>UserAccount</c> (0.3's
/// rule: don't retrofit the core with Kadari-only tenancy). One binding per account. The
/// <c>InsurerTenantGuard</c> reads it to confine an insurer's cohort reads to their own tenant; an account
/// with no binding (an admin) is unconfined.
/// </summary>
public sealed class InsurerUser : AggregateRoot<InsurerUserId>
{
    private InsurerUser() { }   // EF

    private InsurerUser(InsurerUserId id, UserAccountId userAccountId, TenantId tenantId, DateTimeOffset at)
        : base(id)
    {
        UserAccountId = userAccountId;
        TenantId = tenantId;
        GrantedAt = at;
    }

    public UserAccountId UserAccountId { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }

    public static InsurerUser Grant(UserAccountId userAccountId, TenantId tenantId, DateTimeOffset at) =>
        new(InsurerUserId.New(), userAccountId, tenantId, at);

    /// <summary>Move the insurer user to a different tenant (idempotent on an unchanged tenant).</summary>
    public void Reassign(TenantId tenantId, DateTimeOffset at)
    {
        TenantId = tenantId;
        GrantedAt = at;
    }
}
