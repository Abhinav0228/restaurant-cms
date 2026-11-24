namespace RestaurantCms.Infrastructure.Caching;

/// <summary>Clears cached public data for a restaurant when its menu, offers or settings change.
/// The API registers an output-cache implementation; the default does nothing.</summary>
public interface ITenantCacheInvalidator
{
    Task InvalidatePublicMenuAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Forgets everything cached about a restaurant (its lookup by id and slug, and its public menu),
    /// so a suspension or plan change takes effect on the next request.</summary>
    Task InvalidateTenantAsync(Guid tenantId, string slug, CancellationToken ct = default);

    public static string PublicMenuTag(Guid tenantId) => $"public-menu:{tenantId}";
    public static string TenantIdKey(Guid tenantId) => $"tenant-id:{tenantId}";
    public static string TenantSlugKey(string slug) => $"tenant-slug:{slug.ToLowerInvariant()}";
}

internal sealed class NoCacheInvalidator : ITenantCacheInvalidator
{
    public Task InvalidatePublicMenuAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
    public Task InvalidateTenantAsync(Guid tenantId, string slug, CancellationToken ct = default) => Task.CompletedTask;
}
