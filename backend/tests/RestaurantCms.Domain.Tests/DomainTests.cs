using RestaurantCms.Domain;
using RestaurantCms.Domain.Menu;
using RestaurantCms.Domain.Offers;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Tenancy;
using RestaurantCms.Domain.Orders;
using RestaurantCms.Domain.Reservations;
using RestaurantCms.Domain.Tables;

namespace RestaurantCms.Domain.Tests;

internal static class Make
{
    public static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 30, 0, TimeSpan.Zero);

    public static MenuCategory Category(TimeOnly? from = null, TimeOnly? until = null) =>
        MenuCategory.Create("Mains", null, 0, from, until).Value;

    public static MenuItem Item(MenuCategory c, decimal price = 100, int prep = 10, params ModifierGroup[] groups) =>
        MenuItem.Create(c.Id, "Pizza", null, price, prep, modifierGroups: groups).Value;

    public static ModifierGroup Size() => new()
    {
        Name = "Size", MinSelect = 1, MaxSelect = 1,
        Options = [new() { Name = "Regular", PriceDelta = 0 }, new() { Name = "Large", PriceDelta = 50 }]
    };

    public static (DiningTable Table, TableSession Session) OpenTable()
    {
        var table = DiningTable.Create("T1", "Table 1", 4, "Indoor").Value;
        var session = table.OpenSession(2, null, Now).Value;
        return (table, session);
    }

    public static Order DineInOrder(int qty = 1, decimal price = 100, PricingRules? pricing = null, int prep = 10)
    {
        var c = Category();
        var item = OrderItem.Create(Item(c, price, prep), qty, [], null).Value;
        var (table, session) = OpenTable();
        return Order.Place(1001, OrderType.DineIn, [item], pricing ?? PricingRules.None, Now, table.Id, table.Label, session.Id).Value;
    }
}

public class MenuCategoryTests
{
    [Fact]
    public void Category_without_window_serves_all_day()
    {
        var c = Make.Category();
        Assert.True(c.IsServingAt(new TimeOnly(3, 0)));
        Assert.True(c.IsServingAt(new TimeOnly(23, 59)));
    }

    [Theory]
    [InlineData(6, 59, false)]
    [InlineData(7, 0, true)]
    [InlineData(11, 29, true)]
    [InlineData(11, 30, false)]
    public void Breakfast_window_is_respected(int h, int m, bool expected) =>
        Assert.Equal(expected, Make.Category(new TimeOnly(7, 0), new TimeOnly(11, 30)).IsServingAt(new TimeOnly(h, m)));

    [Theory]
    [InlineData(23, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(12, 0, false)]
    public void Late_night_window_wraps_past_midnight(int h, int m, bool expected) =>
        Assert.Equal(expected, Make.Category(new TimeOnly(22, 0), new TimeOnly(2, 0)).IsServingAt(new TimeOnly(h, m)));

    [Fact]
    public void Inactive_category_never_serves()
    {
        var c = Make.Category();
        c.Update(c.Name, null, false, null, null);
        Assert.False(c.IsServingAt(new TimeOnly(12, 0)));
    }

    [Fact]
    public void Half_window_is_rejected()
    {
        var r = MenuCategory.Create("Brunch", null, 0, new TimeOnly(9, 0), null);
        Assert.True(r.IsFailure);
        Assert.Equal("Category.WindowIncomplete", r.Error!.Code);
    }

    [Fact]
    public void Blank_name_is_rejected() => Assert.True(MenuCategory.Create("  ", null, 0).IsFailure);
}

public class MenuItemTests
{
    [Fact]
    public void Negative_price_is_rejected()
    {
        var c = Make.Category();
        Assert.True(MenuItem.Create(c.Id, "X", null, -1, 5).IsFailure);
    }

    [Fact]
    public void Price_is_rounded_to_two_decimals()
    {
        var c = Make.Category();
        Assert.Equal(10.13m, MenuItem.Create(c.Id, "X", null, 10.125m, 5).Value.Price);
    }

    [Fact]
    public void Required_modifier_must_be_chosen()
    {
        var item = Make.Item(Make.Category(), groups: Make.Size());
        var r = item.ResolveModifiers([]);
        Assert.True(r.IsFailure);
        Assert.Equal("Modifier.TooFew", r.Error!.Code);
    }

    [Fact]
    public void Too_many_options_in_single_choice_group_fails()
    {
        var size = Make.Size();
        var item = Make.Item(Make.Category(), groups: size);
        var r = item.ResolveModifiers(size.Options.Select(o => o.Id).ToList());
        Assert.Equal("Modifier.TooMany", r.Error!.Code);
    }

    [Fact]
    public void Unknown_option_fails()
    {
        var item = Make.Item(Make.Category(), groups: Make.Size());
        Assert.Equal("Modifier.Unknown", item.ResolveModifiers([Guid.NewGuid()]).Error!.Code);
    }

    [Fact]
    public void Valid_selection_returns_options()
    {
        var size = Make.Size();
        var item = Make.Item(Make.Category(), groups: size);
        var large = size.Options[1];
        var r = item.ResolveModifiers([large.Id]);
        Assert.True(r.IsSuccess);
        Assert.Equal("Large", Assert.Single(r.Value).Name);
    }

    [Fact]
    public void Invalid_modifier_group_is_rejected_on_create()
    {
        var bad = new ModifierGroup { Name = "Size", MinSelect = 2, MaxSelect = 1, Options = [new() { Name = "A" }] };
        var c = Make.Category();
        Assert.True(MenuItem.Create(c.Id, "X", null, 10, 5, modifierGroups: [bad]).IsFailure);
    }

    [Fact]
    public void Sold_out_item_is_not_orderable()
    {
        var c = Make.Category();
        var item = Make.Item(c);
        item.SetAvailability(false);
        Assert.Equal("MenuItem.Unavailable", item.EnsureOrderable(c, new TimeOnly(12, 0)).Error!.Code);
    }

    [Fact]
    public void Item_outside_category_window_is_not_orderable()
    {
        var c = Make.Category(new TimeOnly(7, 0), new TimeOnly(11, 30));
        Assert.Equal("MenuItem.OutOfHours", Make.Item(c).EnsureOrderable(c, new TimeOnly(15, 0)).Error!.Code);
    }
}

public class OrderTests
{
    [Fact]
    public void Totals_apply_modifiers_discount_service_and_tax_in_order()
    {
        var size = Make.Size();
        var c = Make.Category();
        var menuItem = Make.Item(c, 200, 10, size);
        var line = OrderItem.Create(menuItem, 2, [size.Options[1]], null).Value; // (200 + 50) × 2 = 500
        var (table, session) = Make.OpenTable();

        var order = Order.Place(1, OrderType.DineIn, [line], new PricingRules(TaxPercent: 5, ServiceChargePercent: 10, DiscountPercent: 10),
            Make.Now, table.Id, table.Label, session.Id).Value;

        Assert.Equal(500m, order.Subtotal);
        Assert.Equal(50m, order.DiscountAmount);       // 10% of 500
        Assert.Equal(45m, order.ServiceChargeAmount);  // 10% of 450
        Assert.Equal(24.75m, order.TaxAmount);         // 5% of 495
        Assert.Equal(519.75m, order.Total);
    }

    [Fact]
    public void Takeaway_has_no_service_charge_and_needs_a_name()
    {
        var c = Make.Category();
        var line = OrderItem.Create(Make.Item(c, 100), 1, [], null).Value;
        Assert.Equal("Order.CustomerRequired",
            Order.Place(1, OrderType.Takeaway, [line], new PricingRules(5, 10), Make.Now).Error!.Code);

        var order = Order.Place(1, OrderType.Takeaway, [line], new PricingRules(5, 10), Make.Now, customerName: "Asha").Value;
        Assert.Equal(0m, order.ServiceChargeAmount);
        Assert.Equal(105m, order.Total);
    }

    [Fact]
    public void Dine_in_order_must_be_linked_to_a_table()
    {
        var line = OrderItem.Create(Make.Item(Make.Category()), 1, [], null).Value;
        Assert.Equal("Order.TableRequired", Order.Place(1, OrderType.DineIn, [line], PricingRules.None, Make.Now).Error!.Code);
    }

    [Fact]
    public void Empty_order_is_rejected() =>
        Assert.Equal("Order.Empty", Order.Place(1, OrderType.Takeaway, [], PricingRules.None, Make.Now, customerName: "A").Error!.Code);

    [Fact]
    public void Happy_path_status_flow_sets_timestamps()
    {
        var o = Make.DineInOrder();
        var t = Make.Now;
        Assert.True(o.ChangeStatus(OrderStatus.Accepted, t.AddMinutes(1)).IsSuccess);
        Assert.True(o.ChangeStatus(OrderStatus.Preparing, t.AddMinutes(2)).IsSuccess);
        Assert.True(o.ChangeStatus(OrderStatus.Ready, t.AddMinutes(12)).IsSuccess);
        Assert.True(o.ChangeStatus(OrderStatus.Served, t.AddMinutes(14)).IsSuccess);
        Assert.Equal(t.AddMinutes(12), o.ReadyAt);
        Assert.False(o.IsOpen);
    }

    [Fact]
    public void Cannot_go_backwards()
    {
        var o = Make.DineInOrder();
        o.ChangeStatus(OrderStatus.Preparing, Make.Now);
        o.ChangeStatus(OrderStatus.Ready, Make.Now);
        Assert.Equal("Order.InvalidTransition", o.ChangeStatus(OrderStatus.Pending, Make.Now).Error!.Code);
    }

    [Fact]
    public void Cancel_requires_reason_and_is_blocked_after_serving()
    {
        var o = Make.DineInOrder();
        Assert.Equal("Order.CancelReason", o.ChangeStatus(OrderStatus.Cancelled, Make.Now).Error!.Code);
        Assert.True(o.ChangeStatus(OrderStatus.Cancelled, Make.Now, "Guest left").IsSuccess);
        Assert.False(o.CountsTowardsBill);

        var served = Make.DineInOrder();
        served.ChangeStatus(OrderStatus.Preparing, Make.Now);
        served.ChangeStatus(OrderStatus.Ready, Make.Now);
        served.ChangeStatus(OrderStatus.Served, Make.Now);
        Assert.True(served.ChangeStatus(OrderStatus.Cancelled, Make.Now, "x").IsFailure);
    }

    [Fact]
    public void Delay_is_flagged_once_after_prep_time_plus_buffer()
    {
        var o = Make.DineInOrder(prep: 10);
        o.ChangeStatus(OrderStatus.Preparing, Make.Now);
        var buffer = TimeSpan.FromMinutes(5);

        Assert.False(o.CheckDelay(Make.Now.AddMinutes(15), buffer));
        Assert.True(o.CheckDelay(Make.Now.AddMinutes(16), buffer));
        Assert.False(o.CheckDelay(Make.Now.AddMinutes(20), buffer)); // already flagged
        Assert.True(o.IsDelayed);

        o.ChangeStatus(OrderStatus.Ready, Make.Now.AddMinutes(21));
        Assert.False(o.IsDelayed);
    }

    [Fact]
    public void Order_lines_snapshot_menu_name_and_price()
    {
        var c = Make.Category();
        var menuItem = Make.Item(c, 100);
        var line = OrderItem.Create(menuItem, 1, [], null).Value;
        menuItem.Update(c.Id, "Renamed", null, 999, 10, null, null, null, null, true);
        Assert.Equal("Pizza", line.Name);
        Assert.Equal(100m, line.UnitPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Quantity_must_be_in_range(int qty) =>
        Assert.True(OrderItem.Create(Make.Item(Make.Category()), qty, [], null).IsFailure);
}

public class TableTests
{
    [Fact]
    public void Opening_a_session_occupies_the_table()
    {
        var (table, session) = Make.OpenTable();
        Assert.Equal(TableStatus.Occupied, table.Status);
        Assert.Equal(session.Id, table.CurrentSessionId);
        Assert.True(session.IsOpen);
    }

    [Fact]
    public void Cannot_open_twice()
    {
        var (table, _) = Make.OpenTable();
        Assert.Equal("Table.AlreadyOpen", table.OpenSession(2, null, Make.Now).Error!.Code);
    }

    [Fact]
    public void Closing_with_unfinished_orders_is_blocked()
    {
        var (table, session) = Make.OpenTable();
        Assert.Equal("Table.UnfinishedOrders", table.CloseSession(session, hasUnfinishedOrders: true, Make.Now).Error!.Code);
        Assert.Equal(TableStatus.Occupied, table.Status);
    }

    [Fact]
    public void Full_lifecycle_free_occupied_cleaning_free()
    {
        var (table, session) = Make.OpenTable();
        Assert.True(table.CloseSession(session, false, Make.Now.AddHours(1)).IsSuccess);
        Assert.Equal(TableStatus.Cleaning, table.Status);
        Assert.Null(table.CurrentSessionId);
        Assert.False(session.IsOpen);

        Assert.Equal("Table.Cleaning", table.EnsureCanTakeOrder().Error!.Code);
        Assert.True(table.MarkClean(Make.Now.AddHours(1)).IsSuccess);
        Assert.Equal(TableStatus.Free, table.Status);
    }

    [Fact]
    public void Reserved_table_can_be_seated()
    {
        var table = DiningTable.Create("T2", "Table 2", 4, "Indoor").Value;
        Assert.True(table.Reserve(Make.Now).IsSuccess);
        Assert.True(table.OpenSession(4, "Riya", Make.Now).IsSuccess);
        Assert.Equal(TableStatus.Occupied, table.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("WAYTOOLONGCODE1")]
    public void Invalid_codes_are_rejected(string code) =>
        Assert.True(DiningTable.Create(code, "Table", 2, "Indoor").IsFailure);

    [Fact]
    public void Code_is_uppercased() => Assert.Equal("T9", DiningTable.Create("t9", "Table 9", 2, "Indoor").Value.Code);
}

public class ReservationTests
{
    private static Reservation Booking(DateTimeOffset at) =>
        Reservation.Request("Asha", "+91 98765 43210", "asha@example.com", 4, at, null, Make.Now).Value;

    [Fact]
    public void Too_soon_or_invalid_contact_is_rejected()
    {
        Assert.Equal("Reservation.TooSoon", Reservation.Request("A", "9876543210", null, 2, Make.Now.AddMinutes(5), null, Make.Now).Error!.Code);
        Assert.Equal("Reservation.Phone", Reservation.Request("A", "12", null, 2, Make.Now.AddDays(1), null, Make.Now).Error!.Code);
        Assert.Equal("Reservation.Email", Reservation.Request("A", "9876543210", "nope", 2, Make.Now.AddDays(1), null, Make.Now).Error!.Code);
    }

    [Fact]
    public void Time_is_stored_in_utc()
    {
        var ist = new DateTimeOffset(2026, 11, 10, 20, 0, 0, TimeSpan.FromHours(5.5));
        var r = Booking(ist);
        Assert.Equal(TimeSpan.Zero, r.ReservedFor.Offset);
        Assert.Equal(ist, r.ReservedFor);
    }

    [Fact]
    public void No_show_only_after_grace_period()
    {
        var at = Make.Now.AddHours(3);
        var r = Booking(at);
        r.Confirm(null, Make.Now);
        Assert.False(r.TryMarkNoShow(at.AddMinutes(19)));
        Assert.True(r.TryMarkNoShow(at.AddMinutes(20)));
        Assert.Equal(ReservationStatus.NoShow, r.Status);
    }

    [Fact]
    public void Seated_guest_is_never_a_no_show_and_cannot_be_cancelled()
    {
        var at = Make.Now.AddHours(3);
        var r = Booking(at);
        r.Seat(at);
        Assert.False(r.TryMarkNoShow(at.AddHours(1)));
        Assert.True(r.Cancel(at).IsFailure);
        Assert.True(r.Complete(at.AddHours(2)).IsSuccess);
    }
}

public class OfferAndSettingsTests
{
    [Fact]
    public void Offer_goes_live_inside_window_only()
    {
        var offer = Offer.Create("Happy hour", null, 20, Make.Now.AddHours(1), Make.Now.AddHours(3), true, Make.Now).Value;
        Assert.False(offer.IsLive);
        Assert.True(offer.SyncLive(Make.Now.AddHours(1)));
        Assert.True(offer.IsLive);
        Assert.False(offer.SyncLive(Make.Now.AddHours(2))); // unchanged
        Assert.True(offer.SyncLive(Make.Now.AddHours(3)));
        Assert.False(offer.IsLive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(95)]
    public void Offer_discount_must_be_sensible(int pct) =>
        Assert.True(Offer.Create("X", null, pct, Make.Now, Make.Now.AddDays(1), true, Make.Now).IsFailure);

    [Fact]
    public void Settings_convert_utc_to_restaurant_local_time()
    {
        var s = RestaurantSettings.CreateDefault(); // Asia/Kolkata, UTC+5:30
        Assert.Equal(new TimeOnly(15, 0), s.LocalTime(Make.Now));
        Assert.Equal(new DateOnly(2026, 11, 9), s.LocalDate(Make.Now));
    }

    [Fact]
    public void Settings_default_to_navy_palette_and_reject_unknown_ones()
    {
        var s = RestaurantSettings.CreateDefault();
        Assert.Equal("navy-pink", s.Palette);
        Assert.Equal("Settings.Palette", s.Update("X", null, "INR", 5, 0, "Asia/Kolkata", null, null, null, true, 5, Make.Now, "rainbow").Error!.Code);
        Assert.True(s.Update("X", null, "INR", 5, 0, "Asia/Kolkata", null, null, null, true, 5, Make.Now, "charcoal-blue").IsSuccess);
        Assert.Equal("charcoal-blue", s.Palette);
        // Leaving the palette out keeps the current one
        s.Update("X", null, "INR", 5, 0, "Asia/Kolkata", null, null, null, true, 5, Make.Now);
        Assert.Equal("charcoal-blue", s.Palette);
    }

    [Fact]
    public void Settings_reject_unknown_time_zone()
    {
        var s = RestaurantSettings.CreateDefault();
        var r = s.Update("X", null, "INR", 5, 0, "Mars/Olympus", null, null, null, true, 5, Make.Now);
        Assert.Equal("Settings.TimeZone", r.Error!.Code);
    }
}
