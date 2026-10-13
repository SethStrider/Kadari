namespace Kadari.Application.Common.Pagination;

/// <summary>
/// Standard paged list output for list queries (API design standard, #3).
/// Carries the page slice plus the metadata a client needs to page through the full set.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> From(IReadOnlyList<T> items, PageRequest request, long totalCount) =>
        new(items, request.Page, request.PageSize, totalCount);
}
