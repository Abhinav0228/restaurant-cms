using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Orders;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(AppDbContext db, ITenantContext tenant) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) => db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<PagedResult<Order>> QueryAsync(OrderFilter f, PageRequest page, CancellationToken ct = default)
    {
        var query = db.Orders.AsQueryable();
        if (f.Status is { } status) query = query.Where(o => o.Status == status);
        if (f.TableId is { } tableId) query = query.Where(o => o.TableId == tableId);
        if (f.FromUtc is { } from) { var x = from.ToUniversalTime(); query = query.Where(o => o.PlacedAt >= x); }
        if (f.ToUtc is { } to) { var x = to.ToUniversalTime(); query = query.Where(o => o.PlacedAt < x); }
        if (f.ActiveOnly)
            query = query.Where(o => o.Status != OrderStatus.Served && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.PlacedAt).Skip(page.Skip).Take(page.SafeSize).ToListAsync(ct);
        return new PagedResult<Order>(items, page.SafePage, page.SafeSize, total);
    }

    public Task<List<Order>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default) =>
        db.Orders.Where(o => o.TableSessionId == sessionId).OrderBy(o => o.PlacedAt).ToListAsync(ct);

    public Task<List<Order>> GetBySessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default) =>
        db.Orders.Where(o => o.TableSessionId != null && sessionIds.Contains(o.TableSessionId.Value)).ToListAsync(ct);

    public Task<List<Order>> GetInStatusesAsync(IReadOnlyCollection<OrderStatus> statuses, CancellationToken ct = default) =>
        db.Orders.Where(o => statuses.Contains(o.Status)).OrderBy(o => o.PlacedAt).ToListAsync(ct);

    public Task<List<Order>> GetPlacedBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
    {
        var f = fromUtc.ToUniversalTime();
        var t = toUtc.ToUniversalTime();
        return db.Orders.Where(o => o.PlacedAt >= f && o.PlacedAt < t).ToListAsync(ct);
    }

    /// <summary>Atomic per-restaurant counter (an upsert that returns the new value), safe under concurrency.</summary>
    /// An INSERT ... RETURNING is non-composable, so it is materialised as-is (ToListAsync) rather than
    /// composed over with SingleAsync, which EF would wrap in a SELECT subquery and reject.
    public async Task<int> NextNumberAsync(CancellationToken ct = default)
    {
        var values = await db.Database.SqlQuery<int>($"""
            INSERT INTO tenant_counters ("TenantId", "Name", "Value") VALUES ({tenant.TenantId}, 'order', 1001)
            ON CONFLICT ("TenantId", "Name") DO UPDATE SET "Value" = tenant_counters."Value" + 1
            RETURNING "Value" AS "Value"
            """).ToListAsync(ct);
        return values.Single();
    }

    public void Add(Order order) => db.Orders.Add(order);
}
