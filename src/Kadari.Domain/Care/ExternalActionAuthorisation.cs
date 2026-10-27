using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;

namespace Kadari.Domain.Care;

public readonly record struct ExternalActionAuthorisationId(Guid Value)
{
    public static ExternalActionAuthorisationId New() => new(Guid.CreateVersion7());
}

/// <summary>The external actions an owner can authorise.</summary>
public enum ExternalActionType
{
    WarrantyRegistration,
    CaseHandoff,
}

/// <summary>
/// The owner's explicit authority for one external action on one asset (CARE/12 §8): who granted it, what it covers
/// (the exact fields that may leave KADARI), the version of the wording they agreed to, and when. Distinct from the
/// customer-level <c>KadariConsent</c>: this is per action, per asset. Revocable; never deleted.
/// </summary>
public sealed class ExternalActionAuthorisation : AggregateRoot<ExternalActionAuthorisationId>
{
    public const int MaxScopeLength = 2000;

    private ExternalActionAuthorisation() { }   // EF

    private ExternalActionAuthorisation(ExternalActionAuthorisationId id) : base(id) { }

    public Guid GrantedByAccountId { get; private init; }
    public AssetId AssetId { get; private init; }
    public ExternalActionType ActionType { get; private init; }

    /// <summary>What may be shared and with whom, as shown to the owner (a JSON summary written by the caller).</summary>
    public string ScopeJson { get; private set; } = null!;

    public string TermsVersion { get; private init; } = null!;
    public DateTimeOffset GrantedAt { get; private init; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static ExternalActionAuthorisation Grant(
        Guid grantedByAccountId, AssetId assetId, ExternalActionType actionType, string scopeJson, string termsVersion,
        DateTimeOffset at)
    {
        if (grantedByAccountId == Guid.Empty)
        {
            throw new DomainException("Only a signed-in person can authorise an external action.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(scopeJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(termsVersion);
        if (scopeJson.Length > MaxScopeLength)
        {
            throw new DomainException("An authorisation's scope is too long.");
        }

        return new ExternalActionAuthorisation(ExternalActionAuthorisationId.New())
        {
            GrantedByAccountId = grantedByAccountId,
            AssetId = assetId,
            ActionType = actionType,
            ScopeJson = scopeJson,
            TermsVersion = termsVersion.Trim(),
            GrantedAt = at,
        };
    }

    public bool Covers(ExternalActionType actionType, AssetId assetId, DateTimeOffset at) =>
        ActionType == actionType && AssetId == assetId && RevokedAt is null && at >= GrantedAt;

    public void Revoke(DateTimeOffset at) => RevokedAt ??= at;

    /// <summary>
    /// The owner was erased (POPIA): forget the shared values the scope recorded (name, email, mobile…), keep the
    /// fact that an authority existed, and revoke it. Idempotent.
    /// </summary>
    public void Redact(DateTimeOffset at)
    {
        ScopeJson = """{"redacted":true}""";
        Revoke(at);
    }
}
