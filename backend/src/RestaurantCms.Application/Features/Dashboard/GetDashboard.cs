using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Orders;
using RestaurantCms.Domain.Reporting;

namespace RestaurantCms.Application.Features.Dashboard;

public sealed record DashboardDto(decimal RevenueToday, int OrdersToday, decimal AverageTicket, int ActiveOrders,
    int DelayedOrders, int TablesOccupied, int TablesTotal, int ReservationsToday,
    IReadOnlyList<TopSeller> TopItems, IReadOnlyList<HourlyPoint> OrdersByHour, IReadOnlyList<DailyPoint> Last7Days);

public sealed record HourlyPoint(int Hour, int Orders, decimal Revenue);
public sealed record DailyPoint(DateOnly Date, int Orders, decimal Revenue);

public static class GetDashboard
{
    private static readonly OrderStatus[] ActiveStatuses = [OrderStatus.Pending, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready];

    public sealed record Query : IQuery<DashboardDto>;

    internal sealed class Handler(IOrderRepository orders, ITableRepository tables, IReservationRepository reservations,
        ISalesSnapshotRepository snapshots, ISettingsRepository settings, IClock clock) : IQueryHandler<Query, DashboardDto>
    {
        public async Task<Result<DashboardDto>> Handle(Query query, CancellationToken ct)
        {
            var s = await settings.GetAsync(ct);
            var now = clock.UtcNow;
            var tz = TimeZoneInfo.FindSystemTimeZoneById(s.TimeZoneId);
            var today = s.LocalDate(now);
            var (fromUtc, toUtc) = Reporting.DayRangeUtc(today, tz);

            var billable = (await orders.GetPlacedBetweenAsync(fromUtc, toUtc, ct)).Where(o => o.Status != OrderStatus.Cancelled).ToList();
            var revenue = billable.Sum(o => o.Total);
            var active = await orders.GetInStatusesAsync(ActiveStatuses, ct);
            var allTables = (await tables.GetAllAsync(ct)).Where(t => t.IsActive).ToList();
            var todaysReservations = await reservations.QueryAsync(fromUtc, toUtc, null, ct);
            var history = await snapshots.GetRangeAsync(today.AddDays(-6), today.AddDays(-1), ct);

            return new DashboardDto(
                revenue,
                billable.Count,
                billable.Count == 0 ? 0 : Math.Round(revenue / billable.Count, 2),
                active.Count,
                active.Count(o => o.IsDelayed),
                allTables.Count(t => t.Status == TableStatus.Occupied),
                allTables.Count,
                todaysReservations.Count(r => r.Status != ReservationStatus.Cancelled),
                Reporting.TopSellers(billable, 5),
                billable.GroupBy(o => TimeZoneInfo.ConvertTime(o.PlacedAt, tz).Hour)
                    .Select(g => new HourlyPoint(g.Key, g.Count(), g.Sum(o => o.Total))).OrderBy(p => p.Hour).ToList(),
                Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).Select(d =>
                {
                    if (d == today) return new DailyPoint(d, billable.Count, revenue);
                    var snap = history.FirstOrDefault(h => h.Date == d);
                    return new DailyPoint(d, snap?.OrderCount ?? 0, snap?.Revenue ?? 0);
                }).ToList());
        }
    }
}

internal static class Reporting
{
    public static List<TopSeller> TopSellers(IEnumerable<Order> billable, int take) =>
        billable.SelectMany(o => o.Items)
            .GroupBy(i => i.Name)
            .Select(g => new TopSeller(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
            .OrderByDescending(t => t.Quantity).ThenByDescending(t => t.Revenue)
            .Take(take).ToList();

    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) DayRangeUtc(DateOnly date, TimeZoneInfo tz)
    {
        var startLocal = date.ToDateTime(TimeOnly.MinValue);
        var endLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal)).ToUniversalTime(),
            new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal)).ToUniversalTime());
    }
}
