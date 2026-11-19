using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Orders;
using RestaurantCms.Domain.Tables;

namespace RestaurantCms.Application.Features.Tables;

public sealed record TableDto(Guid Id, string Code, string Label, int Seats, string Area, TableStatus Status,
    int GridX, int GridY, bool IsActive, Guid? CurrentSessionId, int OpenOrderCount, decimal RunningTotal,
    DateTimeOffset? SeatedAt, int? Guests);

public sealed record TableSessionDto(Guid Id, Guid TableId, int Guests, string? WaiterName, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt);

/// <summary>Everything the table drawer needs: the table, its visit, every order placed during it, and the bill.</summary>
public sealed record TableDetailDto(TableDto Table, TableSessionDto? Session, IReadOnlyList<OrderDto> Orders, BillDto? Bill);

public sealed record BillDto(decimal Subtotal, decimal Discount, decimal ServiceCharge, decimal Tax, decimal Total, int OrderCount, int ItemCount);

public sealed record PublicTableDto(string Code, string Label, string RestaurantName, string Currency,
    bool QrOrderingEnabled, bool CanOrder, string? Message);

public sealed record TableFields(string Code, string Label, int Seats, string Area, int GridX, int GridY, bool IsActive = true);

public static class TableMapping
{
    public static TableDto ToDto(this DiningTable t, TableSession? session = null, IEnumerable<Order>? sessionOrders = null)
    {
        var orders = (sessionOrders ?? []).ToList();
        return new TableDto(t.Id, t.Code, t.Label, t.Seats, t.Area, t.Status, t.GridX, t.GridY, t.IsActive, t.CurrentSessionId,
            orders.Count(o => o.IsOpen), orders.Where(o => o.CountsTowardsBill).Sum(o => o.Total), session?.OpenedAt, session?.Guests);
    }

    public static TableSessionDto ToDto(this TableSession s) => new(s.Id, s.TableId, s.Guests, s.WaiterName, s.OpenedAt, s.ClosedAt);

    public static BillDto ToBill(this IEnumerable<Order> orders)
    {
        var billable = orders.Where(o => o.CountsTowardsBill).ToList();
        return new BillDto(billable.Sum(o => o.Subtotal), billable.Sum(o => o.DiscountAmount), billable.Sum(o => o.ServiceChargeAmount),
            billable.Sum(o => o.TaxAmount), billable.Sum(o => o.Total), billable.Count, billable.Sum(o => o.Items.Sum(i => i.Quantity)));
    }
}

/// <summary>Builds the read models for tables (shared by queries and event handlers).</summary>
internal sealed class TableViews(ITableRepository tables, IOrderRepository orders)
{
    public async Task<TableDetailDto> DetailAsync(DiningTable table, CancellationToken ct)
    {
        if (table.CurrentSessionId is not { } sid) return new TableDetailDto(table.ToDto(), null, [], null);
        var session = await tables.GetSessionAsync(sid, ct);
        var sessionOrders = await orders.GetBySessionAsync(sid, ct);
        return new TableDetailDto(table.ToDto(session, sessionOrders), session?.ToDto(),
            sessionOrders.OrderByDescending(o => o.PlacedAt).Select(o => o.ToDto()).ToList(), sessionOrders.ToBill());
    }

    public async Task<IReadOnlyList<TableDto>> FloorAsync(CancellationToken ct)
    {
        var all = await tables.GetAllAsync(ct);
        var sessionIds = all.Where(t => t.CurrentSessionId.HasValue).Select(t => t.CurrentSessionId!.Value).ToList();
        var sessions = sessionIds.Count == 0 ? [] : await tables.GetSessionsAsync(sessionIds, ct);
        var sessionOrders = sessionIds.Count == 0 ? [] : await orders.GetBySessionsAsync(sessionIds, ct);
        var sessionById = sessions.ToDictionary(s => s.Id);
        var ordersBySession = sessionOrders.GroupBy(o => o.TableSessionId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        return all.OrderBy(t => t.Area).ThenBy(t => t.GridY).ThenBy(t => t.GridX).ThenBy(t => t.Code)
            .Select(t => t.CurrentSessionId is { } sid
                ? t.ToDto(sessionById.GetValueOrDefault(sid), ordersBySession.GetValueOrDefault(sid))
                : t.ToDto())
            .ToList();
    }
}
