using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> e)
    {
        e.ToTable("staff_users");
        e.Property(x => x.Email).HasMaxLength(160).IsRequired();
        e.Property(x => x.FullName).HasMaxLength(80).IsRequired();
        e.Ignore(x => x.IsPlatformAdmin);
        // Emails are unique platform-wide so sign-in can find the user's restaurant.
        e.HasIndex(x => x.Email).IsUnique();
        e.HasIndex(x => x.TenantId);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
