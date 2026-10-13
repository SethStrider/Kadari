namespace Kadari.Infrastructure.Tasks;

/// <summary>Configures the Kadari ephemeral-scan-frame purge sweep (D8/POPIA), distinct from the KYC
/// <see cref="EvidenceRetentionOptions"/> horizon.</summary>
public sealed class AssetEvidenceRetentionOptions
{
    public const string SectionName = "AssetEvidenceRetention";

    /// <summary>How often the worker purges ephemeral scan frames past their retention window. Default daily;
    /// 1-minute floor.</summary>
    public int SweepIntervalMinutes { get; init; } = 1440;
}
