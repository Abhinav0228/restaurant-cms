using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Platform;
using RestaurantCms.Infrastructure.Caching;
using RestaurantCms.Infrastructure.Identity;
using RestaurantCms.Infrastructure.Jobs;
using RestaurantCms.Infrastructure.Notifications;
using RestaurantCms.Infrastructure.Persistence;
using RestaurantCms.Infrastructure.Persistence.Repositories;
using RestaurantCms.Infrastructure.Realtime;
using RestaurantCms.Infrastructure.Seeding;
using RestaurantCms.Infrastructure.Tenancy;

namespace RestaurantCms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, bool runHangfireServer = true)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddHttpContextAccessor();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITenantCacheInvalidator, NoCacheInvalidator>();

        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<ISalesSnapshotRepository, SalesSnapshotRepository>();

        services.AddSingleton<Application.Abstractions.IPasswordHasher, IdentityPasswordHasher>();
        services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.Section))
            .Validate(o => o.Key.Length >= 32, "Jwt:Key must be at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddSignalR();
        services.AddScoped<IRealtimeNotifier, SignalRNotifier>();
        services.AddScoped<INotificationSender, LoggingNotificationSender>();
        services.AddScoped<ITenantStarterContent, TenantStarterContent>();
        services.AddSingleton<DbSeeder>();

        services.AddHangfire(h => h
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));
        if (runHangfireServer) services.AddHangfireServer(o => o.WorkerCount = Math.Max(2, Environment.ProcessorCount));
        services.AddScoped<IJobScheduler, HangfireJobScheduler>();
        services.AddTransient<TenantJobRunner>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        return services;
    }
}
