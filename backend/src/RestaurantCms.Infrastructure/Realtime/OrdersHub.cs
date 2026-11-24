using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Application.Features.Tables;
using RestaurantCms.Infrastructure.Identity;

namespace RestaurantCms.Infrastructure.Realtime;

/// <summary>Staff screens connect here. Each connection joins only its own restaurant's group,
/// so updates never leak between tenants.</summary>
[Authorize]
public sealed class OrdersHub : Hub
{
    public const string Path = "/hubs/orders";

    public static string GroupFor(Guid tenantId) => $"tenant:{tenantId}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirst(Claims.TenantId)?.Value, out var tenantId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(tenantId));
        await base.OnConnectedAsync();
    }
}

internal sealed class SignalRNotifier(IHubContext<OrdersHub> hub, ITenantContext tenant) : IRealtimeNotifier
{
    private IClientProxy Group => hub.Clients.Group(OrdersHub.GroupFor(tenant.TenantId));

    public Task OrderCreatedAsync(OrderDto order, CancellationToken ct = default) => Group.SendAsync("orderCreated", order, ct);
    public Task OrderUpdatedAsync(OrderDto order, CancellationToken ct = default) => Group.SendAsync("orderUpdated", order, ct);
    public Task OrderDelayedAsync(OrderDto order, CancellationToken ct = default) => Group.SendAsync("orderDelayed", order, ct);
    public Task TableUpdatedAsync(TableDto table, CancellationToken ct = default) => Group.SendAsync("tableUpdated", table, ct);
    public Task ReservationsChangedAsync(CancellationToken ct = default) => Group.SendAsync("reservationsChanged", ct);
}
