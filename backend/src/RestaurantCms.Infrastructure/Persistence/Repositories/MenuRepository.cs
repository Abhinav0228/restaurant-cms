using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Menu;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class MenuRepository(AppDbContext db) : IMenuRepository
{
    public Task<List<MenuCategory>> GetCategoriesAsync(CancellationToken ct = default) =>
        db.MenuCategories.OrderBy(c => c.SortOrder).ToListAsync(ct);

    public Task<MenuCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default) =>
        db.MenuCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> CategoryHasItemsAsync(Guid categoryId, CancellationToken ct = default) =>
        db.MenuItems.AnyAsync(i => i.CategoryId == categoryId, ct);

    public void AddCategory(MenuCategory category) => db.MenuCategories.Add(category);
    public void RemoveCategory(MenuCategory category) => db.MenuCategories.Remove(category);

    public Task<List<MenuItem>> GetItemsAsync(Guid? categoryId = null, string? search = null, CancellationToken ct = default)
    {
        var query = db.MenuItems.AsQueryable();
        if (categoryId is { } cid) query = query.Where(i => i.CategoryId == cid);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(i => EF.Functions.ILike(i.Name, pattern) || (i.Description != null && EF.Functions.ILike(i.Description, pattern)));
        }
        return query.OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToListAsync(ct);
    }

    public Task<MenuItem?> GetItemAsync(Guid id, CancellationToken ct = default) => db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<List<MenuItem>> GetItemsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        db.MenuItems.Where(i => ids.Contains(i.Id)).ToListAsync(ct);

    public void AddItem(MenuItem item) => db.MenuItems.Add(item);
    public void RemoveItem(MenuItem item) => db.MenuItems.Remove(item);
}
