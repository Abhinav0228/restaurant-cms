using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Reporting;

public sealed class DailySalesSnapshot : TenantEntity
{
    private DailySalesSnapshot() { }

    public DateOnly Date { get; private set; }
    public int OrderCount { get; private set; }
    public int CancelledCount { get; private set; }
    public decimal Revenue { get; private set; }
    public decimal AverageTicket { get; private set; }
    public List<TopSeller> TopItems { get; private set; } = [];

    public static DailySalesSnapshot Create(DateOnly date, int orderCount, int cancelledCount, decimal revenue,
        IEnumerable<TopSeller> topItems) => new()
    {
        Date = date,
        OrderCount = orderCount,
        CancelledCount = cancelledCount,
        Revenue = Money.Round(revenue),
        AverageTicket = orderCount == 0 ? 0 : Money.Round(revenue / orderCount),
        TopItems = topItems.ToList()
    };
}

public sealed record TopSeller(string Name, int Quantity, decimal Revenue);
