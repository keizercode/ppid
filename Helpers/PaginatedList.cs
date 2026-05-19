namespace PermintaanData.Helpers;

/// <summary>
/// Wrapper hasil query terpaginasi.
/// Digunakan oleh semua controller list agar tidak ada unbounded ToListAsync().
/// </summary>
public sealed class PaginatedList<T>
{
    public IReadOnlyList<T> Items      { get; }
    public int              Page       { get; }
    public int              PageSize   { get; }
    public int              TotalCount { get; }
    public int              TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool             HasPrev    => Page > 1;
    public bool             HasNext    => Page < TotalPages;

    private PaginatedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items      = items;
        Page       = page;
        PageSize   = pageSize;
        TotalCount = totalCount;
    }

    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source, int page, int pageSize = 50)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(source);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(source.Skip((page - 1) * pageSize).Take(pageSize));

        return new PaginatedList<T>(items, page, pageSize, total);
    }
}
