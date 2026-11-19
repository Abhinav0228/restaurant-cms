using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Features.Orders;

public static class GetOrders
{
    public sealed record Query(OrderFilter Filter, PageRequest Page) : IQuery<PagedResult<OrderDto>>;

    internal sealed class Handler(IOrderRepository orders) : IQueryHandler<Query, PagedResult<OrderDto>>
    {
        public async Task<Result<PagedResult<OrderDto>>> Handle(Query query, CancellationToken ct) =>
            (await orders.QueryAsync(query.Filter, query.Page, ct)).Map(o => o.ToDto());
    }
}

public static class GetOrder
{
    public sealed record Query(Guid Id) : IQuery<OrderDto>;

    internal sealed class Handler(IOrderRepository orders) : IQueryHandler<Query, OrderDto>
    {
        public async Task<Result<OrderDto>> Handle(Query query, CancellationToken ct) =>
            await orders.GetAsync(query.Id, ct) is { } order ? order.ToDto() : Error.NotFound("Order", query.Id);
    }
}
