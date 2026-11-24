using RestaurantCms.Infrastructure.Identity;

namespace RestaurantCms.Api.Security;

public static class Policies
{
    /// <summary>Any restaurant staff member (has a tenant claim).</summary>
    public const string TenantStaff = "tenant-staff";
    public const string Floor = "floor";
    public const string Manager = "manager";
    public const string Admin = "admin";
    public const string PlatformAdmin = "platform-admin";
    public const string PublicRateLimit = "public";

    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationBuilder auth) => auth
        .AddPolicy(TenantStaff, p => p.RequireClaim(Claims.TenantId).RequireRole("Admin", "Manager", "Waiter", "Kitchen"))
        .AddPolicy(Floor, p => p.RequireClaim(Claims.TenantId).RequireRole("Admin", "Manager", "Waiter"))
        .AddPolicy(Manager, p => p.RequireClaim(Claims.TenantId).RequireRole("Admin", "Manager"))
        .AddPolicy(Admin, p => p.RequireClaim(Claims.TenantId).RequireRole("Admin"))
        .AddPolicy(PlatformAdmin, p => p.RequireRole("PlatformAdmin"));
}
