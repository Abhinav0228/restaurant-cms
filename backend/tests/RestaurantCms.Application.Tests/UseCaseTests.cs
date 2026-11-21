using RestaurantCms.Application.Common;
using RestaurantCms.Application.Features.Menu;
using RestaurantCms.Application.Features.Orders;
using RestaurantCms.Application.Features.Platform;
using RestaurantCms.Application.Features.Tables;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Application.Tests;

public class PipelineTests
{
    [Fact]
    public async Task Invalid_command_is_rejected_with_field_errors_before_reaching_the_handler()
    {
        var host = new TestHost();
        var result = await host.Send(new CreateMenuItem.Command(new MenuItemFields(Guid.Empty, "", null, -5, 10, null, "not a url", null, null)));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("name", result.Error.Fields!.Keys);
        Assert.Contains("price", result.Error.Fields.Keys);
        Assert.Contains("imageUrl", result.Error.Fields.Keys);
        Assert.Equal(0, host.Saves);
    }

    [Fact]
    public async Task Every_handler_resolves_from_the_container()
    {
        var host = new TestHost();
        // A query that touches several ports proves the DI graph is complete.
        var floor = await host.Ask(new GetFloor.Query());
        Assert.True(floor.IsSuccess);
    }
}

public class OrderingTests
{
    [Fact]
    public async Task Qr_order_opens_a_visit_and_shows_on_the_table()
    {
        var host = new TestHost();
        var mains = host.AddCategory("Mains");
        var pizza = host.AddItem(mains, "Pizza", 300);
        var table = host.AddTable("T4");

        var placed = await host.Send(new PlaceQrOrder.Command("t4", "Asha", null, [new OrderLine(pizza.Id, 2, null, null)]));

        Assert.True(placed.IsSuccess, placed.Error?.Message);
        Assert.Equal(OrderStatus.Pending, placed.Value.Status);
        Assert.Equal(1001, placed.Value.Number);
        Assert.Equal(TableStatus.Occupied, table.Status);
        Assert.Contains("orderCreated:1001", host.Events);

        var detail = await host.Ask(new GetTableDetail.Query(table.Id));
        Assert.Single(detail.Value.Orders);
        Assert.Equal(placed.Value.Total, detail.Value.Bill!.Total);
    }

    [Fact]
    public async Task Qr_order_is_refused_when_ordering_is_turned_off()
    {
        var host = new TestHost();
        var pizza = host.AddItem(host.AddCategory("Mains"), "Pizza", 300);
        host.AddTable("T1");
        var st = host.Settings;
        st.Update(st.Name, st.Tagline, st.Currency, st.TaxPercent, st.ServiceChargePercent, st.TimeZoneId, st.Address, st.Phone,
            st.OpeningHours, qrOrderingEnabled: false, st.DelayBufferMinutes, host.UtcNow);

        var placed = await host.Send(new PlaceQrOrder.Command("T1", null, null, [new OrderLine(pizza.Id, 1, null, null)]));

        Assert.True(placed.IsFailure);
        Assert.Equal("Qr.Disabled", placed.Error!.Code);
    }

    [Fact]
    public async Task Unknown_menu_item_is_rejected()
    {
        var host = new TestHost();
        host.AddTable("T1");
        var placed = await host.Send(new PlaceQrOrder.Command("T1", null, null, [new OrderLine(Guid.NewGuid(), 1, null, null)]));
        Assert.True(placed.IsFailure);
    }

    [Fact]
    public async Task Status_change_is_pushed_to_staff_screens()
    {
        var host = new TestHost();
        var pizza = host.AddItem(host.AddCategory("Mains"), "Pizza", 300);
        host.AddTable("T1");
        var placed = await host.Send(new PlaceQrOrder.Command("T1", null, null, [new OrderLine(pizza.Id, 1, null, null)]));

        var accepted = await host.Send(new ChangeOrderStatus.Command(placed.Value.Id, OrderStatus.Accepted, null));

        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Contains("orderUpdated:1001:Accepted", host.Events);
    }

    [Fact]
    public async Task Orders_list_is_paged()
    {
        var host = new TestHost();
        var pizza = host.AddItem(host.AddCategory("Mains"), "Pizza", 300);
        host.AddTable("T1");
        for (var i = 0; i < 5; i++)
            await host.Send(new PlaceQrOrder.Command("T1", null, null, [new OrderLine(pizza.Id, 1, null, null)]));

        var page = await host.Ask(new GetOrders.Query(new OrderFilter(), new PageRequest(2, 2)));

        Assert.Equal(5, page.Value.TotalCount);
        Assert.Equal(2, page.Value.Items.Count);
        Assert.Equal(2, page.Value.Page);
    }
}

public class TableTests
{
    [Fact]
    public async Task Settling_closes_the_visit_and_frees_the_table_for_cleaning()
    {
        var host = new TestHost();
        var pizza = host.AddItem(host.AddCategory("Mains"), "Pizza", 300);
        var table = host.AddTable("T2");
        var seated = await host.Send(new SeatTable.Command(table.Id, 3, "Ravi"));
        Assert.True(seated.IsSuccess, seated.Error?.Message);
        var order = await host.Send(new PlaceStaffOrder.Command(OrderType.DineIn, table.Id, null, null, [new OrderLine(pizza.Id, 1, null, null)]));
        foreach (var s in new[] { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served })
            Assert.True((await host.Send(new ChangeOrderStatus.Command(order.Value.Id, s, null))).IsSuccess);

        var settled = await host.Send(new SettleTable.Command(table.Id));

        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.Equal(TableStatus.Cleaning, table.Status);
        Assert.False(host.Sessions.Single().IsOpen);
        Assert.Contains(host.Events, e => e.StartsWith("tableUpdated:T2"));
    }

    [Fact]
    public async Task Plan_table_limit_is_enforced()
    {
        var host = new TestHost(TenantPlan.Starter);
        for (var i = 1; i <= PlanLimits.For(TenantPlan.Starter).MaxTables; i++) host.AddTable($"T{i}");

        var result = await host.Send(new CreateTable.Command(new TableFields("X1", "Extra", 2, "Patio", 0, 0)));

        Assert.True(result.IsFailure);
        Assert.Equal("Plan.TableLimit", result.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_table_code_is_a_conflict()
    {
        var host = new TestHost();
        host.AddTable("T1");
        var result = await host.Send(new CreateTable.Command(new TableFields("t1", "Again", 2, "Indoor", 0, 0)));
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }
}

public class OnboardingTests
{
    [Fact]
    public async Task Creating_a_restaurant_adds_its_owner_settings_and_starter_content()
    {
        var host = new TestHost();
        var result = await host.Send(new CreateTenant.Command("blue-door", "Blue Door Cafe", TenantPlan.Starter,
            "owner@bluedoor.in", "Meera", "Str0ng!pass", WithSampleData: true));

        Assert.True(result.IsSuccess, result.Error?.Message);
        var tenant = host.Tenants.Single(t => t.Slug == "blue-door");
        Assert.Equal(tenant.Id, host.TenantId); // context switched to the new restaurant
        Assert.Contains(host.Staff, u => u.Email == "owner@bluedoor.in" && u.TenantId == tenant.Id && u.Role == StaffRole.Admin);
        Assert.Equal(2, host.SettingsRows.Count);
        Assert.True(host.StarterContentAdded);
    }

    [Fact]
    public async Task Slug_already_taken_is_a_conflict()
    {
        var host = new TestHost();
        var result = await host.Send(new CreateTenant.Command("ember-oak", "Copycat", TenantPlan.Starter, "a@b.in", "A", "Str0ng!pass"));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }
}
