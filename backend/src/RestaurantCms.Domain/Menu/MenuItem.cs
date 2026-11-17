using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Menu;

public sealed class MenuItem : TenantEntity
{
    private MenuItem() { }

    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public string? ImageUrl { get; private set; }
    public List<DietaryTag> Tags { get; private set; } = [];
    public int PrepMinutes { get; private set; } = 10;
    public int? Calories { get; private set; }
    public int SortOrder { get; private set; }

    /// <summary>Visible on the menu at all.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Quick "sold out" toggle used during service.</summary>
    public bool IsAvailable { get; private set; } = true;

    public List<ModifierGroup> ModifierGroups { get; private set; } = [];

    public static Result<MenuItem> Create(Guid categoryId, string name, string? description, decimal price,
        int prepMinutes, IEnumerable<DietaryTag>? tags = null, string? imageUrl = null, int? calories = null,
        IEnumerable<ModifierGroup>? modifierGroups = null, int sortOrder = 0)
    {
        var item = new MenuItem { CategoryId = categoryId, SortOrder = sortOrder };
        var result = item.Update(categoryId, name, description, price, prepMinutes, tags, imageUrl, calories,
            modifierGroups, isActive: true);
        return result.IsFailure ? result.Error! : item;
    }

    public Result Update(Guid categoryId, string name, string? description, decimal price, int prepMinutes,
        IEnumerable<DietaryTag>? tags, string? imageUrl, int? calories, IEnumerable<ModifierGroup>? modifierGroups,
        bool isActive)
    {
        if (categoryId == Guid.Empty)
            return Error.Validation("MenuItem.CategoryRequired", "Choose a category.");
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("MenuItem.NameRequired", "Item name is required.");
        if (name.Trim().Length > 120)
            return Error.Validation("MenuItem.NameTooLong", "Item name must be 120 characters or fewer.");
        if (price < 0)
            return Error.Validation("MenuItem.PriceNegative", "Price cannot be negative.");
        if (price > 1_000_000)
            return Error.Validation("MenuItem.PriceTooHigh", "Price looks too high.");
        if (prepMinutes is < 0 or > 240)
            return Error.Validation("MenuItem.PrepMinutes", "Prep time must be between 0 and 240 minutes.");
        if (calories is < 0)
            return Error.Validation("MenuItem.Calories", "Calories cannot be negative.");

        var groups = modifierGroups?.ToList() ?? [];
        foreach (var group in groups)
        {
            var check = group.Validate();
            if (check.IsFailure) return check;
        }
        if (groups.Select(g => g.Id).Distinct().Count() != groups.Count)
            return Error.Validation("MenuItem.DuplicateGroups", "Modifier groups must be unique.");

        CategoryId = categoryId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Price = Money.Round(price);
        PrepMinutes = prepMinutes;
        Tags = tags?.Distinct().ToList() ?? [];
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        Calories = calories;
        ModifierGroups = groups;
        IsActive = isActive;
        return Result.Success();
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public Result EnsureOrderable(MenuCategory category, TimeOnly localTime)
    {
        if (!IsActive || !IsAvailable)
            return Error.Conflict("MenuItem.Unavailable", $"'{Name}' is not available right now.");
        if (category.Id != CategoryId || !category.IsServingAt(localTime))
            return Error.Conflict("MenuItem.OutOfHours", $"'{Name}' is not served at this time.");
        return Result.Success();
    }

    /// <summary>Validates a guest's modifier choices and returns the chosen options.</summary>
    public Result<IReadOnlyList<ModifierOption>> ResolveModifiers(IReadOnlyCollection<Guid> selectedOptionIds)
    {
        var chosen = new List<ModifierOption>();
        var known = ModifierGroups.SelectMany(g => g.Options).Select(o => o.Id).ToHashSet();
        var unknown = selectedOptionIds.Where(id => !known.Contains(id)).ToList();
        if (unknown.Count > 0)
            return Error.Validation("Modifier.Unknown", $"Unknown option selected for '{Name}'.");

        foreach (var group in ModifierGroups)
        {
            var picked = group.Options.Where(o => selectedOptionIds.Contains(o.Id)).ToList();
            if (picked.Count < group.MinSelect)
                return Error.Validation("Modifier.TooFew", $"Choose at least {group.MinSelect} for '{group.Name}'.");
            if (picked.Count > group.MaxSelect)
                return Error.Validation("Modifier.TooMany", $"Choose at most {group.MaxSelect} for '{group.Name}'.");
            chosen.AddRange(picked);
        }
        return chosen;
    }
}
