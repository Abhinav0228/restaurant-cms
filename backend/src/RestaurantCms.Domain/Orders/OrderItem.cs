using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Menu;

namespace RestaurantCms.Domain.Orders;

/// <summary>A line on an order. Name and prices are snapshotted so later menu edits
/// never change an existing bill.</summary>
public sealed class OrderItem
{
    private OrderItem() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MenuItemId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public string? Notes { get; private set; }
    public int PrepMinutes { get; private set; }
    public List<OrderItemModifier> Modifiers { get; private set; } = [];

    public decimal UnitTotal => UnitPrice + Modifiers.Sum(m => m.PriceDelta);
    public decimal LineTotal => Money.Round(UnitTotal * Quantity);

    public static Result<OrderItem> Create(MenuItem menuItem, int quantity, IEnumerable<ModifierOption> modifiers, string? notes)
    {
        if (quantity is < 1 or > 50)
            return Error.Validation("OrderItem.Quantity", "Quantity must be between 1 and 50.");
        if (notes?.Length > 200)
            return Error.Validation("OrderItem.Notes", "Item notes must be 200 characters or fewer.");

        return new OrderItem
        {
            MenuItemId = menuItem.Id,
            Name = menuItem.Name,
            UnitPrice = menuItem.Price,
            Quantity = quantity,
            PrepMinutes = menuItem.PrepMinutes,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Modifiers = modifiers.Select(m => new OrderItemModifier(m.Name, m.PriceDelta)).ToList()
        };
    }

    /// <summary>Rebuilds a stored line (used by persistence when reading JSON).</summary>
    public static OrderItem Restore(Guid id, Guid menuItemId, string name, decimal unitPrice, int quantity,
        string? notes, int prepMinutes, IEnumerable<OrderItemModifier>? modifiers) => new()
    {
        Id = id,
        MenuItemId = menuItemId,
        Name = name,
        UnitPrice = unitPrice,
        Quantity = quantity,
        Notes = notes,
        PrepMinutes = prepMinutes,
        Modifiers = modifiers?.ToList() ?? []
    };
}

public sealed record OrderItemModifier(string Name, decimal PriceDelta);
