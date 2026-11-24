using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Menu;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> e)
    {
        e.ToTable("menu_categories");
        e.Property(x => x.Name).HasMaxLength(80).IsRequired();
        e.Property(x => x.Description).HasMaxLength(400);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.TenantId, x.SortOrder });
    }
}

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> e)
    {
        e.ToTable("menu_items");
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.Description).HasMaxLength(600);
        e.Property(x => x.ImageUrl).HasMaxLength(500);
        e.Property(x => x.Tags).HasJsonConversion();
        e.Property(x => x.ModifierGroups).HasJsonConversion();
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<MenuCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.TenantId, x.CategoryId, x.SortOrder });
    }
}
