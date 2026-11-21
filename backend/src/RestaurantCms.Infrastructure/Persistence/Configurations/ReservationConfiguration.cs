using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> e)
    {
        e.ToTable("reservations");
        e.Property(x => x.GuestName).HasMaxLength(80).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        e.Property(x => x.Email).HasMaxLength(160);
        e.Property(x => x.Notes).HasMaxLength(500);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.TenantId, x.ReservedFor });
    }
}
