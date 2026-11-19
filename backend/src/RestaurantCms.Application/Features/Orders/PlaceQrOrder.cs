using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Features.Orders;

/// <summary>An order placed by a guest after scanning the table's QR code. It starts as "New"
/// so staff can accept it, and is linked to the table's current visit.</summary>
public static class PlaceQrOrder
{
    public sealed record Command(string TableCode, string? CustomerName, string? Notes, IReadOnlyList<OrderLine> Lines) : ICommand<OrderDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator()
        {
            Required("tableCode", x => x.TableCode);
            Include(x => x.Lines, new OrderLinesRules());
            MaxLength("customerName", x => x.CustomerName, 80);
            MaxLength("notes", x => x.Notes, 500);
        }
    }

    internal sealed class Handler(ITableRepository tables, ISettingsRepository settings, OrderPlacement placement)
        : ICommandHandler<Command, OrderDto>
    {
        public async Task<Result<OrderDto>> Handle(Command command, CancellationToken ct)
        {
            if (!(await settings.GetAsync(ct)).QrOrderingEnabled)
                return Error.Conflict("Qr.Disabled", "Ordering from the table is turned off right now. Please ask a staff member.");

            var table = await tables.GetByCodeAsync(command.TableCode, ct);
            if (table is null) return Error.NotFound("Table", command.TableCode);

            var placed = await placement.PlaceAsync(OrderType.QrDineIn, table, 1, command.CustomerName, command.Notes,
                command.Lines, OrderStatus.Pending, ct);
            return placed.IsSuccess ? placed.Value.ToDto() : placed.Error!;
        }
    }
}
