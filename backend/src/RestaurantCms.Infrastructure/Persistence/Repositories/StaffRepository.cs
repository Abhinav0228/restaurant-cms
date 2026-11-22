using Microsoft.EntityFrameworkCore;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;

namespace RestaurantCms.Infrastructure.Persistence.Repositories;

internal sealed class StaffRepository(AppDbContext db, ITenantContext tenant) : IStaffRepository
{
    public Task<StaffUser?> FindForSignInAsync(string email, CancellationToken ct = default) =>
        db.Staff.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => db.Staff.AnyAsync(u => u.Email == email, ct);

    public Task<List<StaffUser>> GetForTenantAsync(CancellationToken ct = default) =>
        db.Staff.Where(u => u.TenantId == tenant.TenantId).ToListAsync(ct);

    public Task<int> CountForTenantAsync(CancellationToken ct = default) =>
        db.Staff.CountAsync(u => u.TenantId == tenant.TenantId, ct);

    public void Add(StaffUser user) => db.Staff.Add(user);
}
