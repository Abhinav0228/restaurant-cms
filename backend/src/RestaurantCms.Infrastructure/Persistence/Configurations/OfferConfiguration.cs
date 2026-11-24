using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Reporting;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> e)
    {
        e.ToTable("offers");
        e.Property(x => x.Title).HasMaxLength(100).IsRequired();
        e.Property(x => x.Description).HasMaxLength(400);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
