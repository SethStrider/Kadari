using Kadari.Domain.Abstractions;

namespace Kadari.Domain.CareRegistry;

public readonly record struct SourceEvidenceId(Guid Value);

/// <summary>Where a registry fact comes from.</summary>
public enum SourceType
{
    WebPage,
    Document,
    ResearchWorkbook,
    OperatorNote,
}

/// <summary>
/// The provenance of registry knowledge (CARE/12 §4): a manufacturer's warranty page, a retailer policy, a document,
/// the founder's research workbook. Rules and reference rows point here so "How KADARI knows this" always has an
/// answer. Platform knowledge — never customer data.
/// </summary>
public sealed class SourceEvidence : Entity<SourceEvidenceId>
{
    public const int MaxTitleLength = 200;
    public const int MaxUrlLength = 1000;

    private SourceEvidence() { }   // EF

    private SourceEvidence(SourceEvidenceId id, SourceType type, string title, string? url, DateTimeOffset retrievedAt)
        : base(id)
    {
        Type = type;
        Title = title;
        Url = url;
        RetrievedAt = retrievedAt;
    }

    public SourceType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Url { get; private set; }

    /// <summary>When KADARI last read the source.</summary>
    public DateTimeOffset RetrievedAt { get; private set; }

    public static SourceEvidence Record(
        SourceEvidenceId id, SourceType type, string title, string? url, DateTimeOffset retrievedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var cleanTitle = title.Trim();
        var cleanUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();

        if (cleanTitle.Length > MaxTitleLength || cleanUrl?.Length > MaxUrlLength)
        {
            throw new DomainException("A source title or URL is too long.");
        }

        if (cleanUrl is not null && !Uri.TryCreate(cleanUrl, UriKind.Absolute, out _))
        {
            throw new DomainException($"'{cleanUrl}' is not an absolute URL.");
        }

        return new SourceEvidence(id, type, cleanTitle, cleanUrl, retrievedAt);
    }

    /// <summary>The source was read again (a re-import that saw it unchanged).</summary>
    public void Reverify(DateTimeOffset at)
    {
        if (at > RetrievedAt)
        {
            RetrievedAt = at;
        }
    }
}
