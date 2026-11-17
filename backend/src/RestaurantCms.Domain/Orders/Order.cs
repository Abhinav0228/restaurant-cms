using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Orders;

public sealed class Order : TenantEntity
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Accepted] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Ready, OrderStatus.Cancelled],
        [OrderStatus.Ready] = [OrderStatus.Served, OrderStatus.Cancelled],
        [OrderStatus.Served] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = []
    };

    // Not readonly: EF Core materializes this field from the jsonb "items" column.
    private List<OrderItem> _items = [];

    private Order() { }

    public int Number { get; private set; }
    public OrderType Type { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public Guid? TableId { get; private set; }
    public string? TableLabel { get; private set; }
    public Guid? TableSessionId { get; private set; }
    public string? CustomerName { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public decimal Subtotal { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal ServiceChargeAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal Total { get; private set; }

    /// <summary>Longest prep time of any line: used to detect delayed orders.</summary>
    public int ExpectedPrepMinutes { get; private set; }
    public bool IsDelayed { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? PreparingAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? ServedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }

    public bool IsOpen => Status is not (OrderStatus.Served or OrderStatus.Completed or OrderStatus.Cancelled);
    public bool CountsTowardsBill => Status != OrderStatus.Cancelled;

    public static Result<Order> Place(int number, OrderType type, IReadOnlyList<OrderItem> items, PricingRules pricing,
        DateTimeOffset now, Guid? tableId = null, string? tableLabel = null, Guid? tableSessionId = null,
        string? customerName = null, string? notes = null)
    {
        if (items.Count == 0)
            return Error.Validation("Order.Empty", "Add at least one item to the order.");
        if (items.Count > 100)
            return Error.Validation("Order.TooLarge", "An order can have at most 100 lines.");
        if (type != OrderType.Takeaway && (tableId is null || tableSessionId is null))
            return Error.Validation("Order.TableRequired", "Dine-in orders must be linked to a table.");
        if (type == OrderType.Takeaway && string.IsNullOrWhiteSpace(customerName))
            return Error.Validation("Order.CustomerRequired", "Takeaway orders need a customer name.");
        if (notes?.Length > 500)
            return Error.Validation("Order.NotesTooLong", "Notes must be 500 characters or fewer.");

        var order = new Order
        {
            Number = number,
            Type = type,
            TableId = tableId,
            TableLabel = tableLabel,
            TableSessionId = tableSessionId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PlacedAt = now,
            CreatedAt = now,
            ExpectedPrepMinutes = items.Max(i => i.PrepMinutes)
        };
        order._items.AddRange(items);
        order.ApplyPricing(pricing);
        order.Raise(new OrderPlaced(order.Id, tableId));
        return order;
    }

    public Result ChangeStatus(OrderStatus next, DateTimeOffset now, string? reason = null)
    {
        if (next == Status) return Result.Success();
        if (!AllowedTransitions[Status].Contains(next))
            return Error.Conflict("Order.InvalidTransition", $"Order #{Number} cannot move from {Status} to {next}.");
        if (next == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
            return Error.Validation("Order.CancelReason", "Give a reason for cancelling.");

        var previous = Status;
        Status = next;
        Raise(new OrderStatusChanged(Id, TableId, previous, next));
        switch (next)
        {
            case OrderStatus.Accepted: AcceptedAt = now; break;
            case OrderStatus.Preparing: PreparingAt = now; AcceptedAt ??= now; break;
            case OrderStatus.Ready: ReadyAt = now; IsDelayed = false; break;
            case OrderStatus.Served: ServedAt = now; break;
            case OrderStatus.Completed: CompletedAt = now; break;
            case OrderStatus.Cancelled: CancelledAt = now; CancelReason = reason!.Trim(); IsDelayed = false; break;
        }
        Touch(now);
        return Result.Success();
    }

    /// <summary>Flags the order as delayed when it has been cooking longer than expected + buffer.
    /// Returns true only when the flag flips on (so the alert is sent once).</summary>
    public bool CheckDelay(DateTimeOffset now, TimeSpan buffer)
    {
        if (IsDelayed || Status is not (OrderStatus.Accepted or OrderStatus.Preparing)) return false;
        var started = PreparingAt ?? AcceptedAt ?? PlacedAt;
        if (now - started <= TimeSpan.FromMinutes(ExpectedPrepMinutes) + buffer) return false;
        IsDelayed = true;
        Touch(now);
        Raise(new OrderDelayed(Id));
        return true;
    }

    private void ApplyPricing(PricingRules pricing)
    {
        Subtotal = Money.Round(_items.Sum(i => i.LineTotal));
        DiscountPercent = pricing.DiscountPercent;
        DiscountAmount = Money.Round(Subtotal * pricing.DiscountPercent / 100m);
        var afterDiscount = Subtotal - DiscountAmount;
        ServiceChargeAmount = Type == OrderType.Takeaway
            ? 0m
            : Money.Round(afterDiscount * pricing.ServiceChargePercent / 100m);
        TaxAmount = Money.Round((afterDiscount + ServiceChargeAmount) * pricing.TaxPercent / 100m);
        Total = afterDiscount + ServiceChargeAmount + TaxAmount;
    }
}

public sealed record PricingRules(decimal TaxPercent, decimal ServiceChargePercent, decimal DiscountPercent = 0)
{
    public static PricingRules None => new(0, 0, 0);
}
