using RestaurantCms.Application.Common;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Application.Features.Tables;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Menu;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Orders;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tables;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Application.Abstractions;

// Ports: implemented by Infrastructure. Every tenant-owned repository is automatically
// scoped to ITenantContext by the persistence layer, so handlers never filter by tenant themselves.

public interface IUnitOfWork
{
    /// <summary>Saves changes and then dispatches the domain events raised by the changed aggregates.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ITenantRepository
{
    Task<Tenant?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<List<Tenant>> GetAllAsync(CancellationToken ct = default);
    void Add(Tenant tenant);
}

public interface IMenuRepository
{
    Task<List<MenuCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<MenuCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default);
    Task<bool> CategoryHasItemsAsync(Guid categoryId, CancellationToken ct = default);
    void AddCategory(MenuCategory category);
    void RemoveCategory(MenuCategory category);

    Task<List<MenuItem>> GetItemsAsync(Guid? categoryId = null, string? search = null, CancellationToken ct = default);
    Task<MenuItem?> GetItemAsync(Guid id, CancellationToken ct = default);
    Task<List<MenuItem>> GetItemsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    void AddItem(MenuItem item);
    void RemoveItem(MenuItem item);
}

public interface ITableRepository
{
    Task<List<DiningTable>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<DiningTable?> GetAsync(Guid id, CancellationToken ct = default);
    Task<DiningTable?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId = null, CancellationToken ct = default);
    void Add(DiningTable table);
    void Remove(DiningTable table);

    Task<TableSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<TableSession>> GetSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default);
    Task<List<TableSession>> GetOpenSessionsOpenedBeforeAsync(DateTimeOffset before, CancellationToken ct = default);
    void AddSession(TableSession session);
}

public interface IOrderRepository
{
    Task<Order?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Order>> QueryAsync(OrderFilter filter, PageRequest page, CancellationToken ct = default);
    Task<List<Order>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<Order>> GetBySessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default);
    Task<List<Order>> GetInStatusesAsync(IReadOnlyCollection<OrderStatus> statuses, CancellationToken ct = default);
    Task<List<Order>> GetPlacedBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);

    /// <summary>Next order number for the current restaurant (each restaurant counts from 1001).</summary>
    Task<int> NextNumberAsync(CancellationToken ct = default);
    void Add(Order order);
}

public interface IReservationRepository
{
    Task<Reservation?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<Reservation>> QueryAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, ReservationStatus? status, CancellationToken ct = default);
    void Add(Reservation reservation);
}

public interface IOfferRepository
{
    Task<List<Offer>> GetAllAsync(CancellationToken ct = default);
    Task<Offer?> GetAsync(Guid id, CancellationToken ct = default);
    void Add(Offer offer);
    void Remove(Offer offer);
}

public interface ISettingsRepository
{
    /// <summary>The current restaurant's settings, created with defaults if missing.</summary>
    Task<RestaurantSettings> GetAsync(CancellationToken ct = default);
    void Add(RestaurantSettings settings);
}

public interface IStaffRepository
{
    /// <summary>Looks across all tenants (emails are unique platform-wide); used only for sign-in.</summary>
    Task<StaffUser?> FindForSignInAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    /// <summary>Staff of the current tenant.</summary>
    Task<List<StaffUser>> GetForTenantAsync(CancellationToken ct = default);
    Task<int> CountForTenantAsync(CancellationToken ct = default);
    void Add(StaffUser user);
}

public interface ISalesSnapshotRepository
{
    Task<DailySalesSnapshot?> GetAsync(DateOnly date, CancellationToken ct = default);
    Task<List<DailySalesSnapshot>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    void Add(DailySalesSnapshot snapshot);
    void Remove(DailySalesSnapshot snapshot);
}

/// <summary>Pushes live updates to the current tenant's staff screens (SignalR in production).</summary>
public interface IRealtimeNotifier
{
    Task OrderCreatedAsync(OrderDto order, CancellationToken ct = default);
    Task OrderUpdatedAsync(OrderDto order, CancellationToken ct = default);
    Task OrderDelayedAsync(OrderDto order, CancellationToken ct = default);
    Task TableUpdatedAsync(TableDto table, CancellationToken ct = default);
    Task ReservationsChangedAsync(CancellationToken ct = default);
}

/// <summary>Schedules one-off background work for the current tenant (Hangfire in production).</summary>
public interface IJobScheduler
{
    void ScheduleReservationReminder(Guid reservationId, DateTimeOffset runAtUtc);
    void ScheduleNoShowCheck(Guid reservationId, DateTimeOffset runAtUtc);
}

public interface INotificationSender
{
    Task SendReservationReminderAsync(Reservation reservation, string restaurantName, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(StaffUser user, Tenant? tenant);
}
