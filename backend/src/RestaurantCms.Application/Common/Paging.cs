namespace RestaurantCms.Application.Common;

public sealed record PageRequest(int Page = 1, int PageSize = 50)
{
    public const int MaxPageSize = 200;
    public int SafePage => Math.Max(1, Page);
    public int SafeSize => Math.Clamp(PageSize, 1, MaxPageSize);
    public int Skip => (SafePage - 1) * SafeSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) => new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}
