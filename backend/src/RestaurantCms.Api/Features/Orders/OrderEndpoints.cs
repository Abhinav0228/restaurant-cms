using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Domain;

namespace RestaurantCms.Api.Features.Orders;

public sealed record ChangeStatusRequest(OrderStatus Status, string? Reason);
public sealed record GuestOrderRequest(string? CustomerName, string? Notes, IReadOnlyList<OrderLine> Lines);

public sealed class OrderEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        var orders = routes.Staff.MapGroup("/orders").WithTags("Orders");

        orders.MapGet("", async (OrderStatus? status, Guid? tableId, DateTimeOffset? from, DateTimeOffset? to, bool? activeOnly,
                int? page, int? pageSize, IQueryHandler<GetOrders.Query, PagedResult<OrderDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetOrders.Query(new OrderFilter(status, tableId, from, to, activeOnly ?? false),
                new PageRequest(page ?? 1, pageSize ?? 50)), ct)).ToOk());

        orders.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetOrder.Query, OrderDto> h, CancellationToken ct) =>
            (await h.Handle(new GetOrder.Query(id), ct)).ToOk());

        orders.MapPost("", async (PlaceStaffOrder.Command body, ICommandHandler<PlaceStaffOrder.Command, OrderDto> h, CancellationToken ct) =>
            (await h.Handle(body, ct)).ToCreated(o => $"/api/v1/orders/{o.Id}"))
            .RequireAuthorization(Policies.Floor);

        orders.MapPatch("/{id:guid}/status", async (Guid id, ChangeStatusRequest body, ICommandHandler<ChangeOrderStatus.Command, OrderDto> h, CancellationToken ct) =>
            (await h.Handle(new ChangeOrderStatus.Command(id, body.Status, body.Reason), ct)).ToOk());

        routes.Public.MapPost("/tables/{code}/orders", async (string code, GuestOrderRequest body, ICommandHandler<PlaceQrOrder.Command, OrderDto> h, CancellationToken ct) =>
            (await h.Handle(new PlaceQrOrder.Command(code, body.CustomerName, body.Notes, body.Lines), ct)).ToCreated(o => $"/api/v1/orders/{o.Id}"))
            .WithTags("Public");
    }
}
