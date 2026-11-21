using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Menu;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Orders;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tables;
using RestaurantCms.Domain.Tenancy;
using RestaurantCms.Infrastructure.Caching;

namespace RestaurantCms.Infrastructure.Persistence;

/// <summary>
/// EF Core context and unit of work.
///  • Every tenant-owned entity gets a global query filter on the current tenant, so a handler can never
///    read another restaurant's rows by accident.
///  • On save, new tenant-owned rows are stamped with the current tenant, and changing a row that belongs
///    to another tenant throws.
///  • After a successful save the domain events raised by the changed aggregates are dispatched.
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IClock clock,
    IDomainEventDispatcher dispatcher,
    ITenantCacheInvalidator cache) : DbContext(options), IUnitOfWork
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantCounter> TenantCounters => Set<TenantCounter>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<TableSession> TableSessions => Set<TableSession>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<RestaurantSettings> Settings => Set<RestaurantSettings>();
    public DbSet<StaffUser> Staff => Set<StaffUser>();
    public DbSet<DailySalesSnapshot> SalesSnapshots => Set<DailySalesSnapshot>();

    /// <summary>Referenced by the query filters; EF evaluates it per query.</summary>
    private Guid CurrentTenantId => tenant.IsResolved ? tenant.TenantId : Guid.Empty;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<decimal>().HavePrecision(12, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            // e => e.TenantId == this.CurrentTenantId
            var e = Expression.Parameter(entityType.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(e, nameof(ITenantOwned.TenantId)),
                    Expression.Property(Expression.Constant(this), nameof(CurrentTenantId))),
                e);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ITenantOwned.TenantId));
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var menuChanged = false;
        var changedTenants = ChangeTracker.Entries<Tenant>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Select(e => (e.Entity.Id, e.Entity.Slug)).ToList();

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.Entity is TenantEntity owned)
            {
                if (entry.State == EntityState.Added)
                {
                    if (!tenant.IsResolved)
                        throw new InvalidOperationException($"Cannot save a new {entry.Entity.GetType().Name} without a tenant.");
                    owned.AssignTenant(tenant.TenantId);
                }
                else if (entry.State is EntityState.Modified or EntityState.Deleted && tenant.IsResolved && owned.TenantId != tenant.TenantId)
                {
                    throw new InvalidOperationException("Attempted to change data that belongs to another restaurant.");
                }
            }

            if (entry.State == EntityState.Added) entry.Entity.StampCreated(now, currentUser.UserId);
            else if (entry.State == EntityState.Modified) entry.Entity.StampUpdated(now, currentUser.UserId);

            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                entry.Entity is MenuCategory or MenuItem or Offer or RestaurantSettings)
                menuChanged = true;
        }

        var events = ChangeTracker.Entries<Entity>().SelectMany(e => e.Entity.PullDomainEvents()).ToList();
        var saved = await base.SaveChangesAsync(ct);

        if (menuChanged && tenant.IsResolved) await cache.InvalidatePublicMenuAsync(tenant.TenantId, ct);
        foreach (var (id, slug) in changedTenants) await cache.InvalidateTenantAsync(id, slug, ct);
        if (events.Count > 0) await dispatcher.DispatchAsync(events, ct);
        return saved;
    }
}

/// <summary>Per-restaurant counters (e.g. order numbers start at 1001 for every restaurant).</summary>
public sealed class TenantCounter
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}
