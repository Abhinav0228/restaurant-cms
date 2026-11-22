using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class OfferRepository(AppDbContext db) : IOfferRepository
{
    public Task<List<Offer>> GetAllAsync(CancellationToken ct = default) => db.Offers.ToListAsync(ct);
    public Task<Offer?> GetAsync(Guid id, CancellationToken ct = default) => db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
    public void Add(Offer offer) => db.Offers.Add(offer);
    public void Remove(Offer offer) => db.Offers.Remove(offer);
}
