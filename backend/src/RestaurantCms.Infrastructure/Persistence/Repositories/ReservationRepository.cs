using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class ReservationRepository(AppDbContext db) : IReservationRepository
{
    public Task<Reservation?> GetAsync(Guid id, CancellationToken ct = default) => db.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<Reservation>> QueryAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, ReservationStatus? status, CancellationToken ct = default)
    {
        var query = db.Reservations.AsQueryable();
        if (fromUtc is { } from) { var f = from.ToUniversalTime(); query = query.Where(r => r.ReservedFor >= f); }
        if (toUtc is { } to) { var t = to.ToUniversalTime(); query = query.Where(r => r.ReservedFor < t); }
        if (status is { } s) query = query.Where(r => r.Status == s);
        return query.OrderBy(r => r.ReservedFor).Take(500).ToListAsync(ct);
    }

    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
}
