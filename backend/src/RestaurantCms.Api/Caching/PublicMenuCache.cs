using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.OutputCaching;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Infrastructure.Caching;

namespace RestaurantCms.Api.Caching;

/// <summary>Caches a restaurant's public menu for a short time and tags it with the restaurant,
/// so any change to its menu, offers or settings evicts exactly that restaurant's entry.</summary>
public sealed class PublicMenuCachePolicy : IOutputCachePolicy
{
    public const string Name = "public-menu";

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken ct)
    {
        var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = TimeSpan.FromMinutes(2); // "serving now" flags change with the clock
        context.CacheVaryByRules.RouteValueNames = "tenant";
        if (tenant.IsResolved) context.Tags.Add(ITenantCacheInvalidator.PublicMenuTag(tenant.TenantId));
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken ct)
    {
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK) context.AllowCacheStorage = false;
        return ValueTask.CompletedTask;
    }
}

public sealed class OutputCacheInvalidator(IOutputCacheStore store, IMemoryCache memory) : ITenantCacheInvalidator
{
    public Task InvalidatePublicMenuAsync(Guid tenantId, CancellationToken ct = default) =>
        store.EvictByTagAsync(ITenantCacheInvalidator.PublicMenuTag(tenantId), ct).AsTask();

    public Task InvalidateTenantAsync(Guid tenantId, string slug, CancellationToken ct = default)
    {
        memory.Remove(ITenantCacheInvalidator.TenantIdKey(tenantId));
        memory.Remove(ITenantCacheInvalidator.TenantSlugKey(slug));
        return InvalidatePublicMenuAsync(tenantId, ct);
    }
}
