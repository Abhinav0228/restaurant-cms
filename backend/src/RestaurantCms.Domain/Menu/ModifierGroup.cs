using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Menu;

/// <summary>A set of choices on an item, e.g. "Size" (pick 1) or "Extra toppings" (pick up to 3).
/// Stored as JSON on the menu item.</summary>
public sealed class ModifierGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public List<ModifierOption> Options { get; set; } = [];

    public bool IsRequired => MinSelect > 0;

    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return Error.Validation("Modifier.GroupName", "Every modifier group needs a name.");
        if (Options.Count == 0)
            return Error.Validation("Modifier.NoOptions", $"'{Name}' needs at least one option.");
        if (MinSelect < 0 || MaxSelect < 1 || MinSelect > MaxSelect)
            return Error.Validation("Modifier.Range", $"'{Name}': min must be ≤ max and max at least 1.");
        if (MaxSelect > Options.Count)
            return Error.Validation("Modifier.MaxTooHigh", $"'{Name}': max selections exceeds number of options.");
        if (Options.Any(o => string.IsNullOrWhiteSpace(o.Name)))
            return Error.Validation("Modifier.OptionName", $"Every option in '{Name}' needs a name.");
        if (Options.Any(o => o.PriceDelta < 0))
            return Error.Validation("Modifier.NegativePrice", $"Option prices in '{Name}' cannot be negative.");
        return Result.Success();
    }
}

public sealed class ModifierOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
}
