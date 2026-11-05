using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Pagination;

public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public bool HasNextPage => (long)Page * PageSize < TotalCount;

    public bool HasPreviousPage => Page > 1;

    public static async Task<PagedList<T>> CreateAsync(
        IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        int totalCount = await query.CountAsync(cancellationToken);

        long offset = ((long)page - 1) * pageSize;
        List<T> items = offset >= totalCount
            ? []
            : await query.Skip((int)offset).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedList<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
