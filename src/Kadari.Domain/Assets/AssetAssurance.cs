namespace Kadari.Domain.Assets;

/// <summary>
/// How well-evidenced an asset is (deck §16). Cumulative and ordered: each rung builds on the one below,
/// so the headline is the highest rung reached from the bottom up. <b>Derived, never stored</b> — it is a
/// function of the attached evidence, computed at read time (mirrors the platform's
/// <c>EvidenceAssurance.For(rail)</c> derive-don't-store rule).
/// </summary>
public enum AssetAssuranceLevel
{
    None,
    Observed,
    Identified,
    OwnershipSupported,
    ValueSupported,
}

/// <summary>
/// Derives an asset's assurance level from its evidence set. The rules encode D4/D10:
/// <list type="bullet">
/// <item>recognition <c>Confidence</c> is irrelevant here — a 97% identification is still just an
/// identification (a separate axis);</item>
/// <item>only <see cref="EvidenceStrength.Independent"/> ownership/value support lifts the ladder, so an
/// AI/self-asserted value estimate never reaches <see cref="AssetAssuranceLevel.ValueSupported"/>.</item>
/// </list>
/// </summary>
public static class AssetAssurance
{
    public static AssetAssuranceLevel For(IEnumerable<AssetEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var hasImage = false;
        var hasIdentification = false;
        var hasIndependentOwnership = false;
        var hasIndependentValue = false;

        foreach (var e in evidence)
        {
            switch (e.Kind)
            {
                case AssetEvidenceKind.Image:
                    hasImage = true;
                    break;
                case AssetEvidenceKind.Identification:
                    hasIdentification = true;
                    break;
                case AssetEvidenceKind.Ownership when e.Strength == EvidenceStrength.Independent:
                    hasIndependentOwnership = true;
                    break;
                case AssetEvidenceKind.Value when e.Strength == EvidenceStrength.Independent:
                    hasIndependentValue = true;
                    break;
            }
        }

        if (hasImage && hasIdentification && hasIndependentOwnership && hasIndependentValue)
        {
            return AssetAssuranceLevel.ValueSupported;
        }

        if (hasImage && hasIdentification && hasIndependentOwnership)
        {
            return AssetAssuranceLevel.OwnershipSupported;
        }

        if (hasImage && hasIdentification)
        {
            return AssetAssuranceLevel.Identified;
        }

        return hasImage ? AssetAssuranceLevel.Observed : AssetAssuranceLevel.None;
    }
}
