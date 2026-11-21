using RestaurantCms.Domain;
using RestaurantCms.Domain.Orders;

namespace RestaurantCms.Application.Features.Orders;

public sealed record OrderItemDto(Guid Id, Guid MenuItemId, string Name, decimal UnitPrice, int Quantity,
    string? Notes, IReadOnlyList<OrderModifierDto> Modifiers, decimal LineTotal);

public sealed record OrderModifierDto(string Name, decimal PriceDelta);

public sealed record OrderDto(Guid Id, int Number, OrderType Type, OrderStatus Status, Guid? TableId, string? TableLabel,
    Guid? TableSessionId, string? CustomerName, string? Notes, IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal, decimal DiscountPercent, decimal DiscountAmount, decimal ServiceChargeAmount, decimal TaxAmount,
    decimal Total, int ExpectedPrepMinutes, bool IsDelayed, DateTimeOffset PlacedAt, DateTimeOffset? AcceptedAt,
    DateTimeOffset? PreparingAt, DateTimeOffset? ReadyAt, DateTimeOffset? ServedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt, string? CancelReason);

public sealed record OrderLine(Guid MenuItemId, int Quantity, IReadOnlyList<Guid>? ModifierOptionIds, string? Notes);

/// <summary>Filters for the order list.</summary>
public sealed record OrderFilter(OrderStatus? Status = null, Guid? TableId = null, DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null, bool ActiveOnly = false);

public static class OrderMapping
{
    public static OrderDto ToDto(this Order o) =>
        new(o.Id, o.Number, o.Type, o.Status, o.TableId, o.TableLabel, o.TableSessionId, o.CustomerName, o.Notes,
            o.Items.Select(i => new OrderItemDto(i.Id, i.MenuItemId, i.Name, i.UnitPrice, i.Quantity, i.Notes,
                i.Modifiers.Select(m => new OrderModifierDto(m.Name, m.PriceDelta)).ToList(), i.LineTotal)).ToList(),
            o.Subtotal, o.DiscountPercent, o.DiscountAmount, o.ServiceChargeAmount, o.TaxAmount, o.Total,
            o.ExpectedPrepMinutes, o.IsDelayed, o.PlacedAt, o.AcceptedAt, o.PreparingAt, o.ReadyAt, o.ServedAt,
            o.CompletedAt, o.CancelledAt, o.CancelReason);
}
