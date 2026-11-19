using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Features.Menu;

public static class GetCategories
{
    public sealed record Query : IQuery<IReadOnlyList<CategoryDto>>;

    internal sealed class Handler(IMenuRepository menu) : IQueryHandler<Query, IReadOnlyList<CategoryDto>>
    {
        public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(Query query, CancellationToken ct)
        {
            var categories = await menu.GetCategoriesAsync(ct);
            var counts = (await menu.GetItemsAsync(ct: ct)).GroupBy(i => i.CategoryId).ToDictionary(g => g.Key, g => g.Count());
            return categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => c.ToDto(counts.GetValueOrDefault(c.Id))).ToList();
        }
    }
}
