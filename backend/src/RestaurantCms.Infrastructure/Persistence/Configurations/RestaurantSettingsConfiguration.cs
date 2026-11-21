using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class RestaurantSettingsConfiguration : IEntityTypeConfiguration<RestaurantSettings>
{
    public void Configure(EntityTypeBuilder<RestaurantSettings> e)
    {
        e.ToTable("settings");
        e.Property(x => x.Name).HasMaxLength(100);
        e.Property(x => x.Tagline).HasMaxLength(200);
        e.Property(x => x.Currency).HasMaxLength(3);
        e.Property(x => x.TimeZoneId).HasMaxLength(64);
        e.Property(x => x.Address).HasMaxLength(300);
        e.Property(x => x.Phone).HasMaxLength(40);
        e.Property(x => x.OpeningHours).HasMaxLength(200);
        e.Property(x => x.Palette).HasMaxLength(20);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.TenantId).IsUnique();
    }
}
