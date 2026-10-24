namespace Kadari.Domain.Assets;

public readonly record struct AssetHistoryEntryId(Guid Value)
{
    public static AssetHistoryEntryId New() => new(Guid.CreateVersion7());
}

/// <summary>The kind of material thing that happened to an asset. Only what the owner would want in the asset's life
/// story — not every message or evidence upload.</summary>
public enum AssetHistoryKind
{
    Registered,
    Transferred,
    Inherited,
    ReportedStolen,
    Recovered,
    CareEnabled,
    CoverageFound,
    CoverageChanged,
    RegistrationCompleted,
}

/// <summary>
/// One entry in an asset's permanent history — the "one history" that Care, transfer, theft and every later
/// capability write into (CARE/12 §11). Append-only, like <c>AuditEvent</c>: written in the same transaction as the
/// change it records, never updated, never soft-deleted. <see cref="Summary"/> is the human sentence the owner reads;
/// <see cref="SourceType"/>/<see cref="SourceId"/> point at the record that produced it (a coverage decision, a case)
/// so the detail can be opened.
/// </summary>
public sealed class AssetHistoryEntry
{
    public const int MaxSummaryLength = 280;

    private AssetHistoryEntry() { }   // EF

    private AssetHistoryEntry(
        AssetHistoryEntryId id, AssetId assetId, AssetHistoryKind kind, string summary, DateTimeOffset occurredAt,
        string? sourceType, Guid? sourceId)
    {
        Id = id;
        AssetId = assetId;
        Kind = kind;
        Summary = summary;
        OccurredAt = occurredAt;
        SourceType = sourceType;
        SourceId = sourceId;
    }

    public AssetHistoryEntryId Id { get; private init; }
    public AssetId AssetId { get; private init; }
    public AssetHistoryKind Kind { get; private init; }
    public string Summary { get; private init; } = null!;
    public DateTimeOffset OccurredAt { get; private init; }
    public string? SourceType { get; private init; }
    public Guid? SourceId { get; private init; }

    public static AssetHistoryEntry Record(
        AssetId assetId, AssetHistoryKind kind, string summary, DateTimeOffset occurredAt,
        string? sourceType = null, Guid? sourceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        var trimmed = summary.Trim();
        if (trimmed.Length > MaxSummaryLength)
        {
            trimmed = trimmed[..MaxSummaryLength];
        }

        return new AssetHistoryEntry(
            AssetHistoryEntryId.New(), assetId, kind, trimmed, occurredAt,
            string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim(), sourceId);
    }
}
