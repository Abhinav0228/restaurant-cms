using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantCms.Application;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Application.Features.Platform;
using RestaurantCms.Application.Features.Tables;
using RestaurantCms.Domain;
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

namespace RestaurantCms.Application.Tests;

/// <summary>
/// Runs use cases through the real application pipeline (AddApplication: validation decorators, handlers,
/// domain-event dispatch) with every port backed by in-memory lists. One TestHost = one restaurant.
/// </summary>
public sealed class TestHost : IUnitOfWork, ITenantRepository, IMenuRepository, ITableRepository, IOrderRepository,
    IReservationRepository, IOfferRepository, ISettingsRepository, IStaffRepository, ISalesSnapshotRepository,
    IRealtimeNotifier, IJobScheduler, INotificationSender, IClock, IPasswordHasher, ITokenService,
    ITenantContext, ITenantContextSetter, ICurrentUser, ITenantStarterContent
{
    private readonly ServiceProvider _services;

    public TestHost(TenantPlan plan = TenantPlan.Growth)
    {
        var tenant = Tenant.Create("ember-oak", "Ember & Oak", plan, UtcNow).Value;
        Tenants.Add(tenant);
        TenantId = tenant.Id;

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        foreach (var port in GetType().GetInterfaces().Where(i => i.Namespace?.StartsWith("RestaurantCms") == true))
            services.AddSingleton(port, this);
        _services = services.BuildServiceProvider();
    }

    // ---------- driving the application ----------
    public Task<Result<T>> Send<T>(ICommand<T> command)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(T));
        dynamic handler = _services.GetRequiredService(handlerType);
        return handler.Handle((dynamic)command, CancellationToken.None);
    }

    public Task<Result<T>> Ask<T>(IQuery<T> query)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(T));
        dynamic handler = _services.GetRequiredService(handlerType);
        return handler.Handle((dynamic)query, CancellationToken.None);
    }

    public T Service<T>() where T : notnull => _services.GetRequiredService<T>();

    // ---------- state ----------
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 9, 9, 30, 0, TimeSpan.Zero); // 15:00 in Asia/Kolkata
    public List<Tenant> Tenants { get; } = [];
    public List<MenuCategory> Categories { get; } = [];
    public List<MenuItem> Items { get; } = [];
    public List<DiningTable> Tables { get; } = [];
    public List<TableSession> Sessions { get; } = [];
    public List<Order> Orders { get; } = [];
    public List<Reservation> Reservations { get; } = [];
    public List<Offer> OfferList { get; } = [];
    public List<StaffUser> Staff { get; } = [];
    public List<DailySalesSnapshot> Snapshots { get; } = [];
    public List<RestaurantSettings> SettingsRows { get; } = [RestaurantSettings.CreateDefault()];
    public RestaurantSettings Settings => SettingsRows[0];
    public int Saves { get; private set; }
    public List<string> Events { get; } = [];
    public List<(string Kind, Guid Id, DateTimeOffset At)> Scheduled { get; } = [];
    public List<Guid> RemindersSent { get; } = [];
    public bool StarterContentAdded { get; private set; }
    private int _orderNumber = 1000;

    public MenuCategory AddCategory(string name, TimeOnly? from = null, TimeOnly? until = null)
    {
        var c = MenuCategory.Create(name, null, Categories.Count, from, until).Value;
        Categories.Add(c);
        return c;
    }

    public MenuItem AddItem(MenuCategory c, string name, decimal price, int prep = 10, params ModifierGroup[] groups)
    {
        var i = MenuItem.Create(c.Id, name, null, price, prep, modifierGroups: groups).Value;
        Items.Add(i);
        return i;
    }

    public DiningTable AddTable(string code, int seats = 4)
    {
        var t = DiningTable.Create(code, $"Table {code}", seats, "Indoor").Value;
        Tables.Add(t);
        return t;
    }

    // ---------- IUnitOfWork: save + dispatch domain events, like the real DbContext ----------
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Saves++;
        IEnumerable<Entity> all = [.. Categories, .. Items, .. Tables, .. Sessions, .. Orders, .. Reservations, .. OfferList, .. Snapshots, .. Tenants];
        var events = all.SelectMany(e => e.PullDomainEvents()).ToList();
        await Service<IDomainEventDispatcher>().DispatchAsync(events);
        return 1;
    }

    // ---------- tenancy ----------
    public Guid TenantId { get; private set; }
    public string? Slug => "ember-oak";
    public bool IsResolved => TenantId != Guid.Empty;
    public void Set(Guid tenantId, string? slug) => TenantId = tenantId;
    public Guid? UserId => null;
    public string? Name => "Test";
    public StaffRole? Role => StaffRole.Admin;
    public bool IsAuthenticated => true;
    public Task AddAsync(Guid tenantId, CancellationToken ct) { StarterContentAdded = true; return Task.CompletedTask; }

    Task<Tenant?> ITenantRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));
    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Tenants.FirstOrDefault(t => t.Slug == slug));
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => Task.FromResult(Tenants.Any(t => t.Slug == slug));
    Task<List<Tenant>> ITenantRepository.GetAllAsync(CancellationToken ct) => Task.FromResult(Tenants.ToList());
    public void Add(Tenant tenant) => Tenants.Add(tenant);

    // ---------- menu ----------
    public Task<List<MenuCategory>> GetCategoriesAsync(CancellationToken ct = default) => Task.FromResult(Categories.ToList());
    public Task<MenuCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));
    public Task<bool> CategoryHasItemsAsync(Guid categoryId, CancellationToken ct = default) => Task.FromResult(Items.Any(i => i.CategoryId == categoryId));
    public void AddCategory(MenuCategory category) => Categories.Add(category);
    public void RemoveCategory(MenuCategory category) => Categories.Remove(category);
    public Task<List<MenuItem>> GetItemsAsync(Guid? categoryId = null, string? search = null, CancellationToken ct = default) =>
        Task.FromResult(Items.Where(i => (categoryId == null || i.CategoryId == categoryId) && (search == null || i.Name.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList());
    public Task<MenuItem?> GetItemAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task<List<MenuItem>> GetItemsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => Task.FromResult(Items.Where(i => ids.Contains(i.Id)).ToList());
    public void AddItem(MenuItem item) => Items.Add(item);
    public void RemoveItem(MenuItem item) => Items.Remove(item);

    // ---------- tables ----------
    Task<List<DiningTable>> ITableRepository.GetAllAsync(CancellationToken ct) => Task.FromResult(Tables.ToList());
    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(Tables.Count);
    Task<DiningTable?> ITableRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Tables.FirstOrDefault(t => t.Id == id));
    public Task<DiningTable?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Tables.FirstOrDefault(t => t.Code == code.Trim().ToUpperInvariant()));
    public Task<bool> CodeExistsAsync(string code, Guid? exceptId = null, CancellationToken ct = default) =>
        Task.FromResult(Tables.Any(t => t.Code == code.Trim().ToUpperInvariant() && t.Id != exceptId));
    public void Add(DiningTable table) => Tables.Add(table);
    public void Remove(DiningTable table) => Tables.Remove(table);
    public Task<TableSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default) => Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));
    public Task<List<TableSession>> GetSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default) => Task.FromResult(Sessions.Where(s => sessionIds.Contains(s.Id)).ToList());
    public Task<List<TableSession>> GetOpenSessionsOpenedBeforeAsync(DateTimeOffset before, CancellationToken ct = default) => Task.FromResult(Sessions.Where(s => s.IsOpen && s.OpenedAt < before).ToList());
    public void AddSession(TableSession session) => Sessions.Add(session);

    // ---------- orders ----------
    Task<Order?> IOrderRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));
    public Task<PagedResult<Order>> QueryAsync(OrderFilter f, PageRequest page, CancellationToken ct = default)
    {
        var all = Orders.Where(o => (f.Status == null || o.Status == f.Status) && (f.TableId == null || o.TableId == f.TableId) && (!f.ActiveOnly || o.IsOpen))
            .OrderByDescending(o => o.PlacedAt).ToList();
        return Task.FromResult(new PagedResult<Order>(all.Skip(page.Skip).Take(page.SafeSize).ToList(), page.SafePage, page.SafeSize, all.Count));
    }
    public Task<List<Order>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default) => Task.FromResult(Orders.Where(o => o.TableSessionId == sessionId).ToList());
    public Task<List<Order>> GetBySessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default) => Task.FromResult(Orders.Where(o => o.TableSessionId is { } s && sessionIds.Contains(s)).ToList());
    public Task<List<Order>> GetInStatusesAsync(IReadOnlyCollection<OrderStatus> statuses, CancellationToken ct = default) => Task.FromResult(Orders.Where(o => statuses.Contains(o.Status)).ToList());
    public Task<List<Order>> GetPlacedBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) => Task.FromResult(Orders.Where(o => o.PlacedAt >= fromUtc && o.PlacedAt < toUtc).ToList());
    public Task<int> NextNumberAsync(CancellationToken ct = default) => Task.FromResult(++_orderNumber);
    public void Add(Order order) => Orders.Add(order);

    // ---------- reservations / offers / settings / staff / snapshots ----------
    Task<Reservation?> IReservationRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Reservations.FirstOrDefault(r => r.Id == id));
    public Task<List<Reservation>> QueryAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, ReservationStatus? status, CancellationToken ct = default) =>
        Task.FromResult(Reservations.Where(r => (fromUtc == null || r.ReservedFor >= fromUtc) && (toUtc == null || r.ReservedFor < toUtc) && (status == null || r.Status == status)).ToList());
    public void Add(Reservation reservation) => Reservations.Add(reservation);
    Task<List<Offer>> IOfferRepository.GetAllAsync(CancellationToken ct) => Task.FromResult(OfferList.ToList());
    Task<Offer?> IOfferRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(OfferList.FirstOrDefault(o => o.Id == id));
    public void Add(Offer offer) => OfferList.Add(offer);
    public void Remove(Offer offer) => OfferList.Remove(offer);
    Task<RestaurantSettings> ISettingsRepository.GetAsync(CancellationToken ct) => Task.FromResult(Settings);
    public void Add(RestaurantSettings settings) => SettingsRows.Add(settings);
    public Task<StaffUser?> FindForSignInAsync(string email, CancellationToken ct = default) => Task.FromResult(Staff.FirstOrDefault(u => u.Email == email));
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => Task.FromResult(Staff.Any(u => u.Email == email));
    public Task<List<StaffUser>> GetForTenantAsync(CancellationToken ct = default) => Task.FromResult(Staff.Where(u => u.TenantId == TenantId).ToList());
    public Task<int> CountForTenantAsync(CancellationToken ct = default) => Task.FromResult(Staff.Count(u => u.TenantId == TenantId));
    public void Add(StaffUser user) => Staff.Add(user);
    Task<DailySalesSnapshot?> ISalesSnapshotRepository.GetAsync(DateOnly date, CancellationToken ct) => Task.FromResult(Snapshots.FirstOrDefault(s => s.Date == date));
    public Task<List<DailySalesSnapshot>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) => Task.FromResult(Snapshots.Where(s => s.Date >= from && s.Date <= to).ToList());
    public void Add(DailySalesSnapshot snapshot) => Snapshots.Add(snapshot);
    public void Remove(DailySalesSnapshot snapshot) => Snapshots.Remove(snapshot);

    // ---------- outbound ports ----------
    public Task OrderCreatedAsync(OrderDto order, CancellationToken ct = default) { Events.Add($"orderCreated:{order.Number}"); return Task.CompletedTask; }
    public Task OrderUpdatedAsync(OrderDto order, CancellationToken ct = default) { Events.Add($"orderUpdated:{order.Number}:{order.Status}"); return Task.CompletedTask; }
    public Task OrderDelayedAsync(OrderDto order, CancellationToken ct = default) { Events.Add($"orderDelayed:{order.Number}"); return Task.CompletedTask; }
    public Task TableUpdatedAsync(TableDto table, CancellationToken ct = default) { Events.Add($"tableUpdated:{table.Code}:{table.Status}"); return Task.CompletedTask; }
    public Task ReservationsChangedAsync(CancellationToken ct = default) { Events.Add("reservationsChanged"); return Task.CompletedTask; }
    public void ScheduleReservationReminder(Guid reservationId, DateTimeOffset runAtUtc) => Scheduled.Add(("reminder", reservationId, runAtUtc));
    public void ScheduleNoShowCheck(Guid reservationId, DateTimeOffset runAtUtc) => Scheduled.Add(("noshow", reservationId, runAtUtc));
    public Task SendReservationReminderAsync(Reservation reservation, string restaurantName, CancellationToken ct = default) { RemindersSent.Add(reservation.Id); return Task.CompletedTask; }
    public string Hash(string password) => "hashed:" + password;
    public bool Verify(string hash, string password) => hash == "hashed:" + password;
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(StaffUser user, Tenant? tenant) => ($"token:{user.Email}:{tenant?.Slug}", UtcNow.AddHours(12));
}
