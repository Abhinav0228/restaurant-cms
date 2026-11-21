using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Tables;
using RestaurantCms.Domain;

namespace RestaurantCms.Application.Features.Orders;

/// <summary>Turns domain events into live updates on staff screens. The handlers that change orders and
/// tables don't know about SignalR at all; they just raise events.</summary>
internal sealed class RealtimeNotifications(
    IOrderRepository orders,
    ITableRepository tables,
    TableViews views,
    IRealtimeNotifier notifier)
    : IDomainEventHandler<OrderPlaced>,
      IDomainEventHandler<OrderStatusChanged>,
      IDomainEventHandler<OrderDelayed>,
      IDomainEventHandler<TableStateChanged>,
      IDomainEventHandler<ReservationChanged>
{
    public async Task Handle(OrderPlaced e, CancellationToken ct)
    {
        if (await orders.GetAsync(e.OrderId, ct) is { } order) await notifier.OrderCreatedAsync(order.ToDto(), ct);
        await PublishTable(e.TableId, ct);
    }

    public async Task Handle(OrderStatusChanged e, CancellationToken ct)
    {
        if (await orders.GetAsync(e.OrderId, ct) is { } order) await notifier.OrderUpdatedAsync(order.ToDto(), ct);
        await PublishTable(e.TableId, ct);
    }

    public async Task Handle(OrderDelayed e, CancellationToken ct)
    {
        if (await orders.GetAsync(e.OrderId, ct) is { } order) await notifier.OrderDelayedAsync(order.ToDto(), ct);
    }

    public Task Handle(TableStateChanged e, CancellationToken ct) => PublishTable(e.TableId, ct);

    public Task Handle(ReservationChanged e, CancellationToken ct) => notifier.ReservationsChangedAsync(ct);

    private async Task PublishTable(Guid? tableId, CancellationToken ct)
    {
        if (tableId is not { } id || await tables.GetAsync(id, ct) is not { } table) return;
        await notifier.TableUpdatedAsync((await views.DetailAsync(table, ct)).Table, ct);
    }
}
