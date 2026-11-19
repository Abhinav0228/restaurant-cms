using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Features.Tables;

/// <summary>All tables with live session info (guests, open orders, running bill) for the floor plan.</summary>
public static class GetFloor
{
    public sealed record Query : IQuery<IReadOnlyList<TableDto>>;

    internal sealed class Handler(TableViews views) : IQueryHandler<Query, IReadOnlyList<TableDto>>
    {
        public async Task<Result<IReadOnlyList<TableDto>>> Handle(Query query, CancellationToken ct) => Result<IReadOnlyList<TableDto>>.Success(await views.FloorAsync(ct));
    }
}

public static class GetTableDetail
{
    public sealed record Query(Guid TableId) : IQuery<TableDetailDto>;

    internal sealed class Handler(ITableRepository tables, TableViews views) : IQueryHandler<Query, TableDetailDto>
    {
        public async Task<Result<TableDetailDto>> Handle(Query query, CancellationToken ct) =>
            await tables.GetAsync(query.TableId, ct) is { } table ? await views.DetailAsync(table, ct) : Error.NotFound("Table", query.TableId);
    }
}

/// <summary>What a guest's phone shows after scanning the table QR code.</summary>
public static class GetPublicTable
{
    public sealed record Query(string Code) : IQuery<PublicTableDto>;

    internal sealed class Handler(ITableRepository tables, ISettingsRepository settings) : IQueryHandler<Query, PublicTableDto>
    {
        public async Task<Result<PublicTableDto>> Handle(Query query, CancellationToken ct)
        {
            var table = await tables.GetByCodeAsync(query.Code, ct);
            if (table is null) return Error.NotFound("Table", query.Code);
            var s = await settings.GetAsync(ct);
            var canOrder = table.EnsureCanTakeOrder();
            var message = !s.QrOrderingEnabled
                ? "Ordering from the table is off right now — a staff member will take your order."
                : canOrder.IsFailure ? canOrder.Error!.Message : null;
            return new PublicTableDto(table.Code, table.Label, s.Name, s.Currency, s.QrOrderingEnabled, s.QrOrderingEnabled && canOrder.IsSuccess, message);
        }
    }
}

/// <summary>Orders placed at this table during the current visit, for the guest's status list.</summary>
public static class GetGuestTableOrders
{
    public sealed record Query(string Code) : IQuery<IReadOnlyList<OrderDto>>;

    internal sealed class Handler(ITableRepository tables, IOrderRepository orders) : IQueryHandler<Query, IReadOnlyList<OrderDto>>
    {
        public async Task<Result<IReadOnlyList<OrderDto>>> Handle(Query query, CancellationToken ct)
        {
            var table = await tables.GetByCodeAsync(query.Code, ct);
            if (table is null) return Error.NotFound("Table", query.Code);
            if (table.CurrentSessionId is not { } sid) return Array.Empty<OrderDto>();
            return (await orders.GetBySessionAsync(sid, ct)).OrderByDescending(o => o.PlacedAt).Select(o => o.ToDto()).ToList();
        }
    }
}
