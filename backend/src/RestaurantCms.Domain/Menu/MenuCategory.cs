using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Menu;

public sealed class MenuCategory : TenantEntity
{
    private MenuCategory() { }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Optional serving window, e.g. Breakfast 07:00–11:30. Null = all day.</summary>
    public TimeOnly? AvailableFrom { get; private set; }
    public TimeOnly? AvailableUntil { get; private set; }

    public static Result<MenuCategory> Create(string name, string? description, int sortOrder,
        TimeOnly? availableFrom = null, TimeOnly? availableUntil = null)
    {
        var category = new MenuCategory();
        var result = category.Update(name, description, true, availableFrom, availableUntil);
        if (result.IsFailure) return result.Error!;
        category.SortOrder = sortOrder;
        return category;
    }

    public Result Update(string name, string? description, bool isActive, TimeOnly? availableFrom, TimeOnly? availableUntil)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Category.NameRequired", "Category name is required.");
        if (name.Trim().Length > 80)
            return Error.Validation("Category.NameTooLong", "Category name must be 80 characters or fewer.");
        if (availableFrom.HasValue != availableUntil.HasValue)
            return Error.Validation("Category.WindowIncomplete", "Set both 'available from' and 'available until', or neither.");
        if (availableFrom.HasValue && availableFrom == availableUntil)
            return Error.Validation("Category.WindowEmpty", "Serving window start and end cannot be the same.");

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = isActive;
        AvailableFrom = availableFrom;
        AvailableUntil = availableUntil;
        return Result.Success();
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>True when the category is active and <paramref name="localTime"/> is inside its window.
    /// Windows may wrap past midnight (e.g. 22:00–02:00).</summary>
    public bool IsServingAt(TimeOnly localTime)
    {
        if (!IsActive) return false;
        if (AvailableFrom is not { } from || AvailableUntil is not { } until) return true;
        return from < until
            ? localTime >= from && localTime < until
            : localTime >= from || localTime < until;
    }
}
