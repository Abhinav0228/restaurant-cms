using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Abstractions;

/// <summary>A request that changes state. Exactly one handler per command.</summary>
public interface ICommand<TResponse>;

/// <summary>A request that only reads state.</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct);
}

/// <summary>Reacts to a domain event after the change that raised it has been saved.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}

/// <summary>Response type for commands that return nothing.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
