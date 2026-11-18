using Microsoft.Extensions.Logging;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Events;

/// <summary>Finds every IDomainEventHandler for each event and runs it. A failing handler is logged
/// and does not undo the saved change (side effects such as notifications are best-effort).</summary>
public sealed class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
            if (services.GetService(enumerableType) is not IEnumerable<object> handlers) continue;

            foreach (var handler in handlers)
            {
                try
                {
                    await (Task)handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.Handle))!
                        .Invoke(handler, [domainEvent, ct])!;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Domain event handler {Handler} failed for {Event}", handler.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}
