using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Platform;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;
using RestaurantCms.Infrastructure.Persistence;

namespace RestaurantCms.Infrastructure.Seeding;

/// <summary>Applies migrations, then (in development) creates a platform admin and a demo restaurant
/// "ember-oak" with sample data, using the same onboarding use case a real signup goes through.</summary>
public sealed class DbSeeder(IServiceScopeFactory scopes, IConfiguration config, ILogger<DbSeeder> logger)
{
    public const string DemoSlug = "ember-oak";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);
    }

    public async Task SeedDemoAsync(CancellationToken ct = default)
    {
        var password = config["Seed:Password"] ?? "ChangeMe!123";

        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            if (!await db.Staff.AnyAsync(u => u.Role == StaffRole.PlatformAdmin, ct))
            {
                db.Staff.Add(StaffUser.Create(null, "platform@restaurant.local", "Platform admin", StaffRole.PlatformAdmin, hasher.Hash(password)));
                await db.SaveChangesAsync(ct);
            }
            if (await db.Tenants.AnyAsync(t => t.Slug == DemoSlug, ct)) return;
        }

        using (var scope = scopes.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateTenant.Command, TenantDto>>();
            var result = await create.Handle(new CreateTenant.Command(DemoSlug, "Ember & Oak", TenantPlan.Growth,
                "admin@restaurant.local", "Admin", password, WithSampleData: true), ct);
            if (result.IsFailure) { logger.LogError("Demo restaurant could not be created: {Error}", result.Error!.Message); return; }

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var tenantId = result.Value.Id;
            db.Staff.Add(StaffUser.Create(tenantId, "waiter@restaurant.local", "Riya (Waiter)", StaffRole.Waiter, hasher.Hash(password)));
            db.Staff.Add(StaffUser.Create(tenantId, "kitchen@restaurant.local", "Kitchen", StaffRole.Kitchen, hasher.Hash(password)));
            var settings = await scope.ServiceProvider.GetRequiredService<ISettingsRepository>().GetAsync(ct);
            settings.Update("Ember & Oak", "Wood-fired kitchen and coffee bar", "INR", 5, 0, "Asia/Kolkata",
                "14 Lavelle Road, Bengaluru", "+91 80 4000 1234", "Daily 7:00 am – 11:00 pm", true, 5, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        }
        logger.LogWarning("Seeded demo restaurant '{Slug}' and staff accounts with the default password. Change it before going live.", DemoSlug);
    }
}
