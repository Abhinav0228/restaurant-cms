using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class TenantRepository(AppDbContext db) : ITenantRepository
{
    public Task<Tenant?> GetAsync(Guid id, CancellationToken ct = default) => db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var s = slug.Trim().ToLowerInvariant();
        return db.Tenants.FirstOrDefaultAsync(t => t.Slug == s, ct);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
    {
        var s = slug.Trim().ToLowerInvariant();
        return db.Tenants.AnyAsync(t => t.Slug == s, ct);
    }

    public Task<List<Tenant>> GetAllAsync(CancellationToken ct = default) => db.Tenants.ToListAsync(ct);
    public void Add(Tenant tenant) => db.Tenants.Add(tenant);
}
