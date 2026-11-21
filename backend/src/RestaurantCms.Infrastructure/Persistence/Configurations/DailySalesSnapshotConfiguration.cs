using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class DailySalesSnapshotConfiguration : IEntityTypeConfiguration<DailySalesSnapshot>
{
    public void Configure(EntityTypeBuilder<DailySalesSnapshot> e)
    {
        e.ToTable("daily_sales_snapshots");
        e.HasIndex(x => new { x.TenantId, x.Date }).IsUnique();
        e.Property(x => x.TopItems).HasJsonConversion();
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
