using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Dashboard;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Reporting;

namespace RestaurantCms.Application.Features.BackgroundJobs;

/// <summary>
/// Background job logic for ONE restaurant. Infrastructure runs these through Hangfire and sets the tenant
/// context first (recurring jobs loop over every active tenant). Every method is idempotent.
/// Live updates happen through domain events, like everywhere else.
/// </summary>
public sealed class RestaurantJobs(
    IReservationRepository reservations,
    ITableRepository tables,
    IOrderRepository orders,
    IOfferRepository offers,
    ISalesSnapshotRepository snapshots,
    ISettingsRepository settings,
    INotificationSender notifications,
    IUnitOfWork uow,
    IClock clock)
{
    public static readonly TimeSpan StaleSessionAge = TimeSpan.FromHours(6);

    /// <summary>Delayed job, scheduled when a reservation is confirmed (2h before).</summary>
    public async Task SendReservationReminderAsync(Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await reservations.GetAsync(reservationId, ct);
        if (reservation is null || reservation.ReminderSent || reservation.Status != ReservationStatus.Confirmed) return;

        var s = await settings.GetAsync(ct);
        await notifications.SendReservationReminderAsync(reservation, s.Name, ct);
        reservation.MarkReminderSent();
        if (reservation.TableId is { } tid && await tables.GetAsync(tid, ct) is { Status: TableStatus.Free } table)
            table.Reserve(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Delayed job at booking time + grace: marks no-shows and frees the held table.</summary>
    public async Task CheckNoShowAsync(Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await reservations.GetAsync(reservationId, ct);
        if (reservation is null) return;
        var now = clock.UtcNow;
        if (!reservation.TryMarkNoShow(now)) return;
        if (reservation.TableId is { } tid && await tables.GetAsync(tid, ct) is { } table) table.ReleaseReservation(now);
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Recurring (every minute): flags orders cooking longer than expected (raises OrderDelayed).</summary>
    public async Task<int> FlagDelayedOrdersAsync(CancellationToken ct = default)
    {
        var s = await settings.GetAsync(ct);
        var cooking = await orders.GetInStatusesAsync([OrderStatus.Accepted, OrderStatus.Preparing], ct);
        var now = clock.UtcNow;
        var flagged = cooking.Count(o => o.CheckDelay(now, TimeSpan.FromMinutes(s.DelayBufferMinutes)));
        if (flagged > 0) await uow.SaveChangesAsync(ct);
        return flagged;
    }

    /// <summary>Recurring (every 5 minutes): turns scheduled offers on/off at their start/end time.</summary>
    public async Task<int> SyncOffersAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var changed = (await offers.GetAllAsync(ct)).Count(o => o.SyncLive(now));
        if (changed > 0) await uow.SaveChangesAsync(ct);
        return changed;
    }

    /// <summary>Recurring (23:55 restaurant time): stores the day's KPIs. Re-running replaces the snapshot.</summary>
    public async Task<DailySalesSnapshot> CreateDailySnapshotAsync(DateOnly? date = null, CancellationToken ct = default)
    {
        var s = await settings.GetAsync(ct);
        var day = date ?? s.LocalDate(clock.UtcNow);
        var (fromUtc, toUtc) = Reporting.DayRangeUtc(day, TimeZoneInfo.FindSystemTimeZoneById(s.TimeZoneId));
        var placed = await orders.GetPlacedBetweenAsync(fromUtc, toUtc, ct);
        var billable = placed.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        if (await snapshots.GetAsync(day, ct) is { } existing) snapshots.Remove(existing);
        var snapshot = DailySalesSnapshot.Create(day, billable.Count, placed.Count - billable.Count,
            billable.Sum(o => o.Total), Reporting.TopSellers(billable, 10));
        snapshots.Add(snapshot);
        await uow.SaveChangesAsync(ct);
        return snapshot;
    }

    /// <summary>Recurring (hourly): closes visits opened long ago where nothing was ever ordered.</summary>
    public async Task<int> CleanupStaleSessionsAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var freed = 0;
        foreach (var session in await tables.GetOpenSessionsOpenedBeforeAsync(now - StaleSessionAge, ct))
        {
            if ((await orders.GetBySessionAsync(session.Id, ct)).Count > 0) continue;
            var table = await tables.GetAsync(session.TableId, ct);
            if (table is null || table.CurrentSessionId != session.Id) continue;
            if (table.CloseSession(session, hasUnfinishedOrders: false, now).IsFailure) continue;
            table.ForceFree(now);
            freed++;
        }
        if (freed > 0) await uow.SaveChangesAsync(ct);
        return freed;
    }
}
