namespace API.Contracts.Pagination;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages =>
        PageSize > 0
            ? (int)Math.Ceiling(TotalCount / (double)PageSize)
            : 0;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
