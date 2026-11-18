namespace RestaurantCms.Domain.Common;

/// <summary>Base for every aggregate: identity, audit columns and a buffer of domain events
/// that the unit of work dispatches after a successful save.</summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public Guid? UpdatedBy { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    /// <summary>Called by the persistence layer to stamp who created/changed the row.</summary>
    public void StampCreated(DateTimeOffset now, Guid? userId)
    {
        if (CreatedBy is null) CreatedBy = userId;
        if (CreatedAt == default) CreatedAt = now;
    }

    public void StampUpdated(DateTimeOffset now, Guid? userId)
    {
        UpdatedAt = now;
        UpdatedBy = userId;
    }

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Returns and clears the buffered events (used once per save).</summary>
    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}

/// <summary>Marks data that belongs to one restaurant (tenant).</summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}

/// <summary>An entity owned by a tenant. The tenant is stamped by the persistence layer on insert
/// and can never change afterwards, so rows cannot be moved between restaurants.</summary>
public abstract class TenantEntity : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new InvalidOperationException("Tenant id cannot be empty.");
        if (TenantId != Guid.Empty && TenantId != tenantId)
            throw new InvalidOperationException("An entity cannot be moved to another tenant.");
        TenantId = tenantId;
    }
}

/// <summary>Something that happened in the domain. Handlers run after the change is saved.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public static class Money
{
    /// <summary>Rounds a money amount to 2 decimals (half away from zero, as on receipts).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
