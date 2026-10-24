using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care.Events;
using Kadari.Domain.CareRegistry;

namespace Kadari.Domain.Care;

public readonly record struct CareProfileId(Guid Value)
{
    public static CareProfileId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// KADARI Care attached to an existing asset (CARE/12 §2, CARE/14 C1). Not a second asset: it references the
/// <see cref="AssetId"/> — one profile per asset, enforced by a unique index — and holds only what Care adds: the
/// product category Care reasons about, the current coverage position with its records, the next date that needs
/// attention, and the open-case count. Coverage changes only through <see cref="ApplyEvaluation"/>, from a recorded
/// <see cref="CoverageDecision"/>.
/// </summary>
public sealed class CareProfile : AggregateRoot<CareProfileId>
{
    private readonly List<CoverageRecord> _coverage = [];

    private CareProfile() { }   // EF

    private CareProfile(CareProfileId id, AssetId assetId, CareCategory category, DateTimeOffset enabledAt) : base(id)
    {
        AssetId = assetId;
        Category = category;
        EnabledAt = enabledAt;
        CoverageState = CoverageState.Unknown;
    }

    public AssetId AssetId { get; private set; }
    public CareCategory Category { get; private set; }
    public DateTimeOffset EnabledAt { get; private set; }
    public CoverageState CoverageState { get; private set; }

    /// <summary>When the headline cover ends, where KADARI knows it.</summary>
    public DateOnly? CoverageExpiresOn { get; private set; }

    /// <summary>The next date worth the owner's attention (90 and 30 days before expiry, then expiry), or null.</summary>
    public DateOnly? NextAttentionOn { get; private set; }

    /// <summary>Open Asset Cases on this asset. Always 0 until the Case engine (CARE/14 C9) lands.</summary>
    public int OpenCaseCount { get; private set; }

    public DateTimeOffset? LastEvaluatedAt { get; private set; }
    public CoverageDecisionId? LatestDecisionId { get; private set; }

    public IReadOnlyList<CoverageRecord> Coverage => _coverage.AsReadOnly();

    /// <summary>Enable Care on an asset. The caller has proven the asset exists and is the owner's.</summary>
    public static CareProfile Enable(AssetId assetId, CareCategory category, DateTimeOffset at)
    {
        var profile = new CareProfile(CareProfileId.New(), assetId, category, at);
        profile.RaiseDomainEvent(new CareEnabledDomainEvent(profile.Id, assetId, category, at));
        return profile;
    }

    /// <summary>The owner corrected what kind of product this is. Coverage must be re-evaluated afterwards.</summary>
    public void Recategorise(CareCategory category) => Category = category;

    /// <summary>
    /// Adopt a coverage evaluation recorded as <paramref name="decision"/>: one record per kind of cover the
    /// evaluation spoke to (kinds it no longer speaks to drop back to Unknown — a record is never deleted), the
    /// headline state and expiry, and the next attention date. Raises <see cref="CoverageEvaluatedDomainEvent"/>
    /// with the previous state, so history is written only for a material change.
    /// </summary>
    public void ApplyEvaluation(CoverageEvaluation evaluation, CoverageDecision decision, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.AssetId != AssetId)
        {
            throw new DomainException("A coverage decision can only be applied to its own asset's profile.");
        }

        var previousState = CoverageState;
        var previousExpiry = CoverageExpiresOn;

        foreach (var finding in evaluation.Findings)
        {
            var existing = _coverage.FirstOrDefault(r => r.CoverageType == finding.CoverageType);
            if (existing is null)
            {
                _coverage.Add(CoverageRecord.From(finding, decision.Id));
            }
            else
            {
                existing.Apply(finding, decision.Id);
            }
        }

        foreach (var orphan in _coverage.Where(r => evaluation.Findings.All(f => f.CoverageType != r.CoverageType)))
        {
            orphan.Apply(
                new CoverageFinding(orphan.CoverageType, CoverageState.Unknown, null, null, null, false, 0, null, null,
                    false, null, null, "No verified rule covers this any more."),
                decision.Id);
        }

        CoverageState = evaluation.State;
        CoverageExpiresOn = evaluation.Primary?.ExpiresOn;
        NextAttentionOn = NextAttention(CoverageState, CoverageExpiresOn, DateOnly.FromDateTime(at.UtcDateTime));
        LastEvaluatedAt = at;
        LatestDecisionId = decision.Id;

        RaiseDomainEvent(new CoverageEvaluatedDomainEvent(
            Id, AssetId, decision.Id, previousState, CoverageState, previousExpiry, CoverageExpiresOn, at));
    }

    private static DateOnly? NextAttention(CoverageState state, DateOnly? expiresOn, DateOnly today)
    {
        if (expiresOn is not { } expiry || state is not (CoverageState.Active or CoverageState.Expiring or CoverageState.LikelyActive))
        {
            return null;
        }

        DateOnly[] milestones = [expiry.AddDays(-CoverageEvaluator.ExpiringWithinDays), expiry.AddDays(-30), expiry];
        return milestones.Where(d => d >= today).Cast<DateOnly?>().FirstOrDefault();
    }
}
