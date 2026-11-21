using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Tables;

namespace RestaurantCms.Application.Features.Orders;

/// <summary>An order taken by staff (waiter tablet / POS). Dine-in orders join the table's current visit.
/// Staff orders skip "New" and start as Accepted.</summary>
public static class PlaceStaffOrder
{
    public sealed record Command(OrderType Type, Guid? TableId, string? CustomerName, string? Notes,
        IReadOnlyList<OrderLine> Lines, int? Guests = null) : ICommand<OrderDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator()
        {
            Include(x => x.Lines, new OrderLinesRules());
            Rule("type", x => x.Type != OrderType.QrDineIn, "QR orders are placed from the table's QR page.");
            Rule("tableId", x => x.Type != OrderType.DineIn || x.TableId is not null, "Choose a table for dine-in orders.");
            Rule("customerName", x => x.Type != OrderType.Takeaway || !string.IsNullOrWhiteSpace(x.CustomerName), "Takeaway orders need a customer name.");
            MaxLength("notes", x => x.Notes, 500);
        }
    }

    internal sealed class Handler(ITableRepository tables, OrderPlacement placement) : ICommandHandler<Command, OrderDto>
    {
        public async Task<Result<OrderDto>> Handle(Command command, CancellationToken ct)
        {
            DiningTable? table = null;
            if (command.Type == OrderType.DineIn)
            {
                table = await tables.GetAsync(command.TableId!.Value, ct);
                if (table is null) return Error.NotFound("Table", command.TableId);
            }
            var placed = await placement.PlaceAsync(command.Type, table, command.Guests ?? 1, command.CustomerName, command.Notes,
                command.Lines, OrderStatus.Accepted, ct);
            return placed.IsSuccess ? placed.Value.ToDto() : placed.Error!;
        }
    }
}
