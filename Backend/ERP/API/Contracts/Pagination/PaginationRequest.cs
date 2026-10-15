namespace API.Contracts.Pagination;

public class PaginationRequest
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public int Page { get; set; } = 1;

    private int _pageSize = DefaultPageSize;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaximumPageSize);
    }

    public string? Search { get; set; }

    public string? SortBy { get; set; }

    public string SortDirection { get; set; } = "asc";

    public int SafePage => Math.Max(1, Page);

    public int SafePageSize => Math.Clamp(PageSize, 1, MaximumPageSize);

    public string SafeSortDirection =>
        string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";

    public bool IsDescending =>
        string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}
