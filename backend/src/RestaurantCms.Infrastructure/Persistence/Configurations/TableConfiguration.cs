using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Tables;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> e)
    {
        e.ToTable("dining_tables");
        e.Property(x => x.Code).HasMaxLength(12).IsRequired();
        e.Property(x => x.Label).HasMaxLength(40).IsRequired();
        e.Property(x => x.Area).HasMaxLength(40);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        e.HasRowVersion();
    }
}

internal sealed class TableSessionConfiguration : IEntityTypeConfiguration<TableSession>
{
    public void Configure(EntityTypeBuilder<TableSession> e)
    {
        e.ToTable("table_sessions");
        e.Property(x => x.WaiterName).HasMaxLength(80);
        e.Ignore(x => x.IsOpen);
        e.HasOne<DiningTable>().WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.TenantId, x.TableId, x.ClosedAt });
    }
}
