using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Application.Features.Tables;

namespace RestaurantCms.Api.Features.Tables;

public sealed record SeatRequest(int Guests, string? WaiterName);

public sealed class TableEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        var floor = routes.Staff.MapGroup("/tables").WithTags("Tables").RequireAuthorization(Policies.Floor);
        var manage = routes.Staff.MapGroup("/tables").WithTags("Tables").RequireAuthorization(Policies.Manager);

        floor.MapGet("", async (IQueryHandler<GetFloor.Query, IReadOnlyList<TableDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetFloor.Query(), ct)).ToOk());

        floor.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetTableDetail.Query, TableDetailDto> h, CancellationToken ct) =>
            (await h.Handle(new GetTableDetail.Query(id), ct)).ToOk());

        floor.MapPost("/{id:guid}/seat", async (Guid id, SeatRequest body, ICommandHandler<SeatTable.Command, TableDetailDto> h, CancellationToken ct) =>
            (await h.Handle(new SeatTable.Command(id, body.Guests, body.WaiterName), ct)).ToOk());

        floor.MapPost("/{id:guid}/settle", async (Guid id, ICommandHandler<SettleTable.Command, TableDetailDto> h, CancellationToken ct) =>
            (await h.Handle(new SettleTable.Command(id), ct)).ToOk());

        floor.MapPost("/{id:guid}/clean", async (Guid id, ICommandHandler<MarkTableClean.Command, TableDto> h, CancellationToken ct) =>
            (await h.Handle(new MarkTableClean.Command(id), ct)).ToOk());

        manage.MapPost("", async (TableFields body, ICommandHandler<CreateTable.Command, TableDto> h, CancellationToken ct) =>
            (await h.Handle(new CreateTable.Command(body), ct)).ToCreated(t => $"/api/v1/tables/{t.Id}"));

        manage.MapPut("/{id:guid}", async (Guid id, TableFields body, ICommandHandler<UpdateTable.Command, TableDto> h, CancellationToken ct) =>
            (await h.Handle(new UpdateTable.Command(id, body), ct)).ToOk());

        manage.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteTable.Command, Application.Abstractions.Unit> h, CancellationToken ct) =>
            (await h.Handle(new DeleteTable.Command(id), ct)).ToNoContent());

        // Guests scanning a table's QR code
        var guest = routes.Public.MapGroup("/tables/{code}").WithTags("Public");
        guest.MapGet("", async (string code, IQueryHandler<GetPublicTable.Query, PublicTableDto> h, CancellationToken ct) =>
            (await h.Handle(new GetPublicTable.Query(code), ct)).ToOk());
        guest.MapGet("/orders", async (string code, IQueryHandler<GetGuestTableOrders.Query, IReadOnlyList<OrderDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetGuestTableOrders.Query(code), ct)).ToOk());
    }
}
