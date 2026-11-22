using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class SalesSnapshotRepository(AppDbContext db) : ISalesSnapshotRepository
{
    public Task<DailySalesSnapshot?> GetAsync(DateOnly date, CancellationToken ct = default) =>
        db.SalesSnapshots.FirstOrDefaultAsync(s => s.Date == date, ct);

    public Task<List<DailySalesSnapshot>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        db.SalesSnapshots.Where(s => s.Date >= from && s.Date <= to).OrderBy(s => s.Date).ToListAsync(ct);

    public void Add(DailySalesSnapshot snapshot) => db.SalesSnapshots.Add(snapshot);
    public void Remove(DailySalesSnapshot snapshot) => db.SalesSnapshots.Remove(snapshot);
}
