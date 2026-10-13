namespace Kadari.Application.Common.Pagination;

/// <summary>
/// Standard paging/sorting input for list queries (API design standard, #3).
/// Values are clamped to safe bounds so a caller can never request an unbounded page.
/// </summary>
public record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values &lt; 1 clamp to 1.</summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    /// <summary>Items per page. Clamped to [1, <see cref="MaxPageSize"/>].</summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    /// <summary>Optional sort field name (whitelisted per-endpoint by the handler).</summary>
    public string? SortBy { get; init; }

    public bool Descending { get; init; }

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
