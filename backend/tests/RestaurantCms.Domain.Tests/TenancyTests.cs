using RestaurantCms.Domain.Tables;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Domain.Tests;

public class TenantTests
{
    [Theory]
    [InlineData("ember-oak", true)]
    [InlineData("Blue-Door", true)]   // normalised to lower case
    [InlineData("a", false)]          // too short
    [InlineData("-cafe", false)]      // must start with a letter or digit
    [InlineData("cafe_1", false)]     // underscores not allowed
    [InlineData("admin", false)]      // reserved
    public void Slug_rules(string slug, bool ok) =>
        Assert.Equal(ok, Tenant.Create(slug, "Cafe", TenantPlan.Starter, Make.Now).IsSuccess);

    [Fact]
    public void Plans_have_increasing_limits()
    {
        Assert.True(PlanLimits.For(TenantPlan.Starter).MaxTables < PlanLimits.For(TenantPlan.Growth).MaxTables);
        Assert.True(PlanLimits.For(TenantPlan.Growth).MaxStaff < PlanLimits.For(TenantPlan.Enterprise).MaxStaff);
    }

    [Fact]
    public void Suspended_tenant_can_be_reactivated()
    {
        var t = Tenant.Create("ember-oak", "Ember", TenantPlan.Growth, Make.Now).Value;
        t.Suspend(Make.Now);
        Assert.Equal(TenantStatus.Suspended, t.Status);
        t.Activate(Make.Now);
        Assert.Equal(TenantStatus.Active, t.Status);
    }

    [Fact]
    public void Data_cannot_be_moved_to_another_tenant()
    {
        var table = DiningTable.Create("T1", "Table 1", 4, "Indoor").Value;
        var tenant = Guid.NewGuid();
        table.AssignTenant(tenant);
        table.AssignTenant(tenant); // same tenant again is fine
        Assert.Throws<InvalidOperationException>(() => table.AssignTenant(Guid.NewGuid()));
    }
}

public class DomainEventTests
{
    [Fact]
    public void Placing_an_order_raises_OrderPlaced_once()
    {
        var order = Make.DineInOrder();
        var events = order.PullDomainEvents();
        Assert.Contains(events, e => e is OrderPlaced);
        Assert.Empty(order.PullDomainEvents()); // pulled events are cleared
    }

    [Fact]
    public void Seating_a_table_raises_TableStateChanged()
    {
        var (table, _) = Make.OpenTable();
        Assert.Contains(table.PullDomainEvents(), e => e is TableStateChanged { Status: TableStatus.Occupied });
    }
}
