using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantCms.Domain.Orders;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("orders");
        e.Ignore(x => x.Items);
        e.Ignore(x => x.IsOpen);
        e.Ignore(x => x.CountsTowardsBill);
        // Order lines are an immutable snapshot, stored as one jsonb column.
        e.Property<List<OrderItem>>("_items")
            .HasColumnName("items")
            .HasColumnType("jsonb")
            .HasConversion(
                v => OrderItemsJson.Write(v),
                v => OrderItemsJson.Read(v),
                new ValueComparer<List<OrderItem>>(
                    (a, c) => OrderItemsJson.Write(a!) == OrderItemsJson.Write(c!),
                    v => OrderItemsJson.Write(v).GetHashCode(),
                    v => OrderItemsJson.Read(OrderItemsJson.Write(v))));
        e.Property(x => x.TableLabel).HasMaxLength(40);
        e.Property(x => x.CustomerName).HasMaxLength(80);
        e.Property(x => x.Notes).HasMaxLength(500);
        e.Property(x => x.CancelReason).HasMaxLength(200);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.Status });
        e.HasIndex(x => new { x.TenantId, x.PlacedAt });
        e.HasIndex(x => x.TableSessionId);
        e.HasRowVersion();
    }
}

internal static class OrderItemsJson
{
    internal sealed record Row(Guid Id, Guid MenuItemId, string Name, decimal UnitPrice, int Quantity, string? Notes,
        int PrepMinutes, List<OrderItemModifier> Modifiers);

    public static string Write(List<OrderItem> items) => JsonSerializer.Serialize(
        items.Select(i => new Row(i.Id, i.MenuItemId, i.Name, i.UnitPrice, i.Quantity, i.Notes, i.PrepMinutes, i.Modifiers)),
        JsonColumn.Options);

    public static List<OrderItem> Read(string json) =>
        (JsonSerializer.Deserialize<List<Row>>(json, JsonColumn.Options) ?? [])
        .Select(r => OrderItem.Restore(r.Id, r.MenuItemId, r.Name, r.UnitPrice, r.Quantity, r.Notes, r.PrepMinutes, r.Modifiers))
        .ToList();
}
