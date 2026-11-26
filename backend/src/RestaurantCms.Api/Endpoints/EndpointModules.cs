using System.Reflection;
using RestaurantCms.Api.Security;

namespace RestaurantCms.Api.Endpoints;

/// <summary>A feature's HTTP surface. Each feature folder has one module; they are discovered at startup,
/// so adding a feature never means editing a central file.</summary>
public interface IEndpointModule
{
    void Map(ApiRoutes routes);
}

/// <summary>The route groups every module maps into.</summary>
/// <param name="Staff">/api/v1 — signed-in restaurant staff, scoped to their restaurant.</param>
/// <param name="Public">/api/v1/public/{tenant} — guests of one restaurant (menu, QR ordering, bookings).</param>
/// <param name="Anonymous">/api/v1 — no sign-in (e.g. sign-in itself).</param>
/// <param name="Platform">/api/v1/platform — platform administrators (onboarding restaurants).</param>
public sealed record ApiRoutes(RouteGroupBuilder Staff, RouteGroupBuilder Public, RouteGroupBuilder Anonymous, RouteGroupBuilder Platform);

public static class EndpointModuleExtensions
{
    public const string ApiPrefix = "/api/v1";
    public const string TenantRouteKey = "tenant";

    public static WebApplication MapEndpointModules(this WebApplication app)
    {
        var api = app.MapGroup(ApiPrefix);
        var routes = new ApiRoutes(
            Staff: api.MapGroup("").RequireAuthorization(Policies.TenantStaff),
            Public: api.MapGroup($"/public/{{{TenantRouteKey}}}").RequireRateLimiting(Policies.PublicRateLimit).AllowAnonymous(),
            Anonymous: api.MapGroup("").AllowAnonymous(),
            Platform: api.MapGroup("/platform").RequireAuthorization(Policies.PlatformAdmin));

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpointModule).IsAssignableFrom(t)))
            ((IEndpointModule)Activator.CreateInstance(type)!).Map(routes);

        return app;
    }
}
