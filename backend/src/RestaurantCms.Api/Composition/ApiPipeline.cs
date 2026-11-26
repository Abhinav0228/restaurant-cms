using Hangfire;
using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Api.Tenancy;
using RestaurantCms.Infrastructure.Jobs;
using RestaurantCms.Infrastructure.Realtime;
using RestaurantCms.Infrastructure.Seeding;
using Scalar.AspNetCore;

namespace RestaurantCms.Api.Composition;

public static class ApiPipeline
{
    public static async Task<WebApplication> UseApiAsync(this WebApplication app)
    {
        // Database + jobs
        var seeder = app.Services.GetRequiredService<DbSeeder>();
        await seeder.MigrateAsync();
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Seed:Demo", false))
            await seeder.SeedDemoAsync();
        RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        if (!app.Environment.IsDevelopment()) app.UseHsts();

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseMiddleware<TenantResolutionMiddleware>(); // needs route values + the signed-in user
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseOutputCache();

        app.MapOpenApi();
        app.MapScalarApiReference(o => o.WithTitle("Restaurant CMS API"));
        app.MapHealthChecks("/health");
        app.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            DashboardTitle = "Restaurant CMS · Jobs",
            Authorization = [],
            AsyncAuthorization = [new HangfireDashboardAuth(app.Environment)]
        });
        app.MapHub<OrdersHub>(OrdersHub.Path);
        app.MapEndpointModules();
        return app;
    }
}
