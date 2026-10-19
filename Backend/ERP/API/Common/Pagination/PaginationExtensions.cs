using API.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

namespace API.Common.Pagination;

public static class PaginationExtensions
{
    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> query,
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = request.SafePage;
        var pageSize = request.SafePageSize;

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<T>(items, page, pageSize, totalCount);
    }

    public static PagedResponse<T> ToPagedResponse<T>(
        this IEnumerable<T> source,
        PaginationRequest request)
    {
        var list = source as IReadOnlyList<T> ?? source.ToList();
        var page = request.SafePage;
        var pageSize = request.SafePageSize;
        var totalCount = list.Count;

        var items = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResponse<T>(items, page, pageSize, totalCount);
    }
}
