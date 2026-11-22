using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Tables;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class TableRepository(AppDbContext db) : ITableRepository
{
    public Task<List<DiningTable>> GetAllAsync(CancellationToken ct = default) => db.Tables.ToListAsync(ct);
    public Task<int> CountAsync(CancellationToken ct = default) => db.Tables.CountAsync(ct);
    public Task<DiningTable?> GetAsync(Guid id, CancellationToken ct = default) => db.Tables.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<DiningTable?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return db.Tables.FirstOrDefaultAsync(t => t.Code == normalized, ct);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? exceptId = null, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return db.Tables.AnyAsync(t => t.Code == normalized && (exceptId == null || t.Id != exceptId), ct);
    }

    public void Add(DiningTable table) => db.Tables.Add(table);
    public void Remove(DiningTable table) => db.Tables.Remove(table);

    public Task<TableSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        db.TableSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    public Task<List<TableSession>> GetSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default) =>
        db.TableSessions.Where(s => sessionIds.Contains(s.Id)).ToListAsync(ct);

    public Task<List<TableSession>> GetOpenSessionsOpenedBeforeAsync(DateTimeOffset before, CancellationToken ct = default)
    {
        var utc = before.ToUniversalTime();
        return db.TableSessions.Where(s => s.ClosedAt == null && s.OpenedAt < utc).ToListAsync(ct);
    }

    public void AddSession(TableSession session) => db.TableSessions.Add(session);
}
