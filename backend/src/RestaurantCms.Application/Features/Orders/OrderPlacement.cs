using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Orders;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Tables;

namespace RestaurantCms.Application.Features.Orders;

internal sealed class OrderLinesRules : Validator<IReadOnlyList<OrderLine>>
{
    public OrderLinesRules()
    {
        Rule("lines", x => x is { Count: > 0 }, "Add at least one item to the order.");
        Rule("lines", x => x is null || x.Count <= 100, "An order can have at most 100 lines.");
        Rule("lines", x => x is null || x.All(l => l.Quantity is >= 1 and <= 50), "Each quantity must be between 1 and 50.");
        Rule("lines", x => x is null || x.All(l => l.MenuItemId != Guid.Empty), "Every line needs a menu item.");
    }
}

/// <summary>Shared order-taking logic used by staff and QR ordering: validates items against the live menu,
/// attaches dine-in orders to the table's current visit (opening one if needed) and prices the order.</summary>
internal sealed class OrderPlacement(
    IOrderRepository orders,
    ITableRepository tables,
    IMenuRepository menu,
    IOfferRepository offers,
    ISettingsRepository settings,
    IUnitOfWork uow,
    IClock clock)
{
    public async Task<Result<Order>> PlaceAsync(OrderType type, DiningTable? table, int guests, string? customerName,
        string? notes, IReadOnlyList<OrderLine> lines, OrderStatus initialStatus, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var s = await settings.GetAsync(ct);

        var items = await BuildItemsAsync(lines, s, now, ct);
        if (items.IsFailure) return items.Error!;

        TableSession? session = null;
        if (table is not null)
        {
            var canOrder = table.EnsureCanTakeOrder();
            if (canOrder.IsFailure) return canOrder.Error!;
            if (table.CurrentSessionId is { } sid) session = await tables.GetSessionAsync(sid, ct);
            if (session is null)
            {
                var opened = table.OpenSession(Math.Max(1, guests), null, now);
                if (opened.IsFailure) return opened.Error!;
                session = opened.Value;
                tables.AddSession(session);
            }
        }

        var discount = (await offers.GetAllAsync(ct))
            .Where(o => o.IsEnabled && now >= o.StartsAt && now < o.EndsAt)
            .Select(o => o.DiscountPercent).DefaultIfEmpty(0).Max();

        var number = await orders.NextNumberAsync(ct);
        var placed = Order.Place(number, type, items.Value, new PricingRules(s.TaxPercent, s.ServiceChargePercent, discount), now,
            table?.Id, table?.Label, session?.Id, customerName, notes);
        if (placed.IsFailure) return placed.Error!;

        var order = placed.Value;
        if (initialStatus != OrderStatus.Pending) order.ChangeStatus(initialStatus, now);
        orders.Add(order);
        await uow.SaveChangesAsync(ct);
        return order;
    }

    private async Task<Result<List<OrderItem>>> BuildItemsAsync(IReadOnlyList<OrderLine> lines, RestaurantSettings s,
        DateTimeOffset now, CancellationToken ct)
    {
        var ids = lines.Select(l => l.MenuItemId).Distinct().ToList();
        var menuItems = (await menu.GetItemsByIdsAsync(ids, ct)).ToDictionary(i => i.Id);
        var categories = (await menu.GetCategoriesAsync(ct)).ToDictionary(c => c.Id);
        var localTime = s.LocalTime(now);

        var result = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (!menuItems.TryGetValue(line.MenuItemId, out var menuItem) || !categories.TryGetValue(menuItem.CategoryId, out var category))
                return Error.Validation("Order.UnknownItem", "One of the items is no longer on the menu.");

            var orderable = menuItem.EnsureOrderable(category, localTime);
            if (orderable.IsFailure) return orderable.Error!;

            var modifiers = menuItem.ResolveModifiers((line.ModifierOptionIds ?? []).Distinct().ToList());
            if (modifiers.IsFailure) return modifiers.Error!;

            var item = OrderItem.Create(menuItem, line.Quantity, modifiers.Value, line.Notes);
            if (item.IsFailure) return item.Error!;
            result.Add(item.Value);
        }
        return result;
    }
}
