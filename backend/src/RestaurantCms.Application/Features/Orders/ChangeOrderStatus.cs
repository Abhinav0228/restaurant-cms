using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Features.Orders;

/// <summary>Moves an order through the kitchen workflow (New → Accepted → Cooking → Ready → Served).</summary>
public static class ChangeOrderStatus
{
    public sealed record Command(Guid OrderId, OrderStatus Status, string? Reason) : ICommand<OrderDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator()
        {
            Rule("reason", x => x.Status != OrderStatus.Cancelled || !string.IsNullOrWhiteSpace(x.Reason), "Give a reason for cancelling.");
            MaxLength("reason", x => x.Reason, 200);
        }
    }

    internal sealed class Handler(IOrderRepository orders, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, OrderDto>
    {
        public async Task<Result<OrderDto>> Handle(Command command, CancellationToken ct)
        {
            var order = await orders.GetAsync(command.OrderId, ct);
            if (order is null) return Error.NotFound("Order", command.OrderId);
            var changed = order.ChangeStatus(command.Status, clock.UtcNow, command.Reason);
            if (changed.IsFailure) return changed.Error!;
            await uow.SaveChangesAsync(ct);
            return order.ToDto();
        }
    }
}
