using Microsoft.Extensions.Caching.Memory;
using RestaurantCms.Api.Endpoints;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Tenancy;
using RestaurantCms.Infrastructure.Caching;
using RestaurantCms.Infrastructure.Identity;

namespace RestaurantCms.Api.Tenancy;

/// <summary>
/// Decides which restaurant a request belongs to and sets ITenantContext:
///  • public routes  → the {tenant} slug in the URL (/api/v1/public/ember-oak/...)
///  • staff requests → the "tid" claim in the signed token
/// Unknown or suspended restaurants get 404 / 403 before any handler runs.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    public async Task InvokeAsync(HttpContext http, ITenantContextSetter setter, ITenantRepository tenants, IMemoryCache cache)
    {
        if (http.GetRouteValue(EndpointModuleExtensions.TenantRouteKey) is string slug)
        {
            var tenant = await cache.GetOrCreateAsync(ITenantCacheInvalidator.TenantSlugKey(slug), e =>
            {
                e.AbsoluteExpirationRelativeToNow = CacheFor;
                return tenants.GetBySlugAsync(slug, http.RequestAborted);
            });
            if (tenant is null || !tenant.IsActive)
            {
                await Results.Problem(title: "This restaurant could not be found.", statusCode: StatusCodes.Status404NotFound).ExecuteAsync(http);
                return;
            }
            setter.Set(tenant.Id, tenant.Slug);
        }
        else if (Guid.TryParse(http.User.FindFirst(Claims.TenantId)?.Value, out var tenantId))
        {
            var tenant = await cache.GetOrCreateAsync(ITenantCacheInvalidator.TenantIdKey(tenantId), e =>
            {
                e.AbsoluteExpirationRelativeToNow = CacheFor;
                return tenants.GetAsync(tenantId, http.RequestAborted);
            });
            if (tenant is null || tenant.Status == TenantStatus.Suspended)
            {
                await Results.Problem(title: "This restaurant's account is suspended. Contact support.", statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(http);
                return;
            }
            setter.Set(tenant.Id, tenant.Slug);
        }

        await next(http);
    }
}
