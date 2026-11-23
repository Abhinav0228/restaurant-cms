using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.BackgroundJobs;
using RestaurantCms.Domain.Tenancy;
using RestaurantCms.Infrastructure.Persistence;

namespace RestaurantCms.Infrastructure.Jobs;

/// <summary>
/// Entry points that Hangfire calls. Each one runs <see cref="RestaurantJobs"/> inside a fresh DI scope with the
/// tenant context set, either for one restaurant (delayed jobs carry the tenant id) or for every active restaurant
/// (recurring jobs). One failing restaurant never stops the others.
/// </summary>
public sealed class TenantJobRunner(IServiceScopeFactory scopes, ILogger<TenantJobRunner> logger)
{
    public Task SendReservationReminder(Guid tenantId, Guid reservationId, CancellationToken ct) =>
        RunFor(tenantId, j => j.SendReservationReminderAsync(reservationId, ct));

    public Task CheckNoShow(Guid tenantId, Guid reservationId, CancellationToken ct) =>
        RunFor(tenantId, j => j.CheckNoShowAsync(reservationId, ct));

    [DisableConcurrentExecution(60)]
    public Task FlagDelayedOrders(CancellationToken ct) => RunForAll(j => j.FlagDelayedOrdersAsync(ct));

    [DisableConcurrentExecution(300)]
    public Task SyncOffers(CancellationToken ct) => RunForAll(j => j.SyncOffersAsync(ct));

    [DisableConcurrentExecution(300)]
    public Task CleanupStaleSessions(CancellationToken ct) => RunForAll(j => j.CleanupStaleSessionsAsync(ct));

    /// <summary>Hourly: refreshes today's snapshot for every restaurant, and just after local midnight
    /// finalises yesterday's. Each restaurant uses its own time zone.</summary>
    [DisableConcurrentExecution(600)]
    public Task RefreshDailySnapshots(CancellationToken ct) => RunForAll(async (j, sp) =>
    {
        var settings = await sp.GetRequiredService<ISettingsRepository>().GetAsync(ct);
        var now = sp.GetRequiredService<IClock>().UtcNow;
        var today = settings.LocalDate(now);
        if (settings.LocalTime(now).Hour == 0) await j.CreateDailySnapshotAsync(today.AddDays(-1), ct);
        await j.CreateDailySnapshotAsync(today, ct);
    });

    private async Task RunFor(Guid tenantId, Func<RestaurantJobs, Task> work)
    {
        using var scope = scopes.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null || !tenant.IsActive) return;
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().Set(tenant.Id, tenant.Slug);
        await work(scope.ServiceProvider.GetRequiredService<RestaurantJobs>());
    }

    private Task RunForAll(Func<RestaurantJobs, Task> work) => RunForAll((j, _) => work(j));

    private async Task RunForAll(Func<RestaurantJobs, IServiceProvider, Task> work)
    {
        List<Tenant> tenants;
        using (var scope = scopes.CreateScope())
            tenants = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Tenants
                .Where(t => t.Status == TenantStatus.Active).ToListAsync();

        foreach (var tenant in tenants)
        {
            try
            {
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().Set(tenant.Id, tenant.Slug);
                await work(scope.ServiceProvider.GetRequiredService<RestaurantJobs>(), scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job failed for restaurant {TenantSlug}", tenant.Slug);
            }
        }
    }
}

internal sealed class HangfireJobScheduler(IBackgroundJobClient client, ITenantContext tenant) : IJobScheduler
{
    public void ScheduleReservationReminder(Guid reservationId, DateTimeOffset runAtUtc)
    {
        var tenantId = tenant.TenantId;
        client.Schedule<TenantJobRunner>(r => r.SendReservationReminder(tenantId, reservationId, CancellationToken.None), runAtUtc);
    }

    public void ScheduleNoShowCheck(Guid reservationId, DateTimeOffset runAtUtc)
    {
        var tenantId = tenant.TenantId;
        client.Schedule<TenantJobRunner>(r => r.CheckNoShow(tenantId, reservationId, CancellationToken.None), runAtUtc);
    }
}

public static class RecurringJobs
{
    /// <summary>Registers (or updates) the recurring jobs. Safe to call on every startup.</summary>
    public static void Register(IRecurringJobManager jobs)
    {
        jobs.AddOrUpdate<TenantJobRunner>("orders:flag-delayed", r => r.FlagDelayedOrders(CancellationToken.None), Cron.Minutely());
        jobs.AddOrUpdate<TenantJobRunner>("offers:sync", r => r.SyncOffers(CancellationToken.None), "*/5 * * * *");
        jobs.AddOrUpdate<TenantJobRunner>("reports:daily-snapshots", r => r.RefreshDailySnapshots(CancellationToken.None), Cron.Hourly());
        jobs.AddOrUpdate<TenantJobRunner>("tables:cleanup-stale-sessions", r => r.CleanupStaleSessions(CancellationToken.None), Cron.Hourly());
    }
}
