using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantCms.Infrastructure.Persistence.Configurations;

internal static class JsonColumn
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Stores a value as jsonb with a value comparer so in-place edits are tracked.</summary>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property) where T : class, new()
    {
        property.HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<T>(v, Options) ?? new T(),
            new ValueComparer<T>(
                (a, c) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(c, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!));
        return property;
    }

    /// <summary>Optimistic concurrency: Npgsql maps a uint row version to PostgreSQL's xmin system column.</summary>
    public static void HasRowVersion<T>(this EntityTypeBuilder<T> entity) where T : class =>
        entity.Property<uint>("Version").IsRowVersion();
}
