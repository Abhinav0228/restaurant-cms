using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain;

// Domain events. Raised by aggregates, dispatched after SaveChanges by the unit of work.

public sealed record OrderPlaced(Guid OrderId, Guid? TableId) : DomainEvent;

public sealed record OrderStatusChanged(Guid OrderId, Guid? TableId, OrderStatus From, OrderStatus To) : DomainEvent;

public sealed record OrderDelayed(Guid OrderId) : DomainEvent;

public sealed record TableStateChanged(Guid TableId, TableStatus Status) : DomainEvent;

public sealed record ReservationChanged(Guid ReservationId, ReservationStatus Status) : DomainEvent;
