using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class SettingsRepository(AppDbContext db, ITenantContext tenant) : ISettingsRepository
{
    public async Task<RestaurantSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(ct)
            ?? db.Settings.Local.FirstOrDefault(s => s.TenantId == tenant.TenantId || s.TenantId == Guid.Empty);
        if (settings is not null) return settings;
        settings = RestaurantSettings.CreateDefault();
        db.Settings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public void Add(RestaurantSettings settings) => db.Settings.Add(settings);
}
