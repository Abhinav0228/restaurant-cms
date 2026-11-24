using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Platform;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Menu;
using RestaurantCms.Domain.Tables;
using RestaurantCms.Infrastructure.Persistence;

namespace RestaurantCms.Infrastructure.Seeding;

/// <summary>Sample menu and floor plan so a new restaurant can try everything straight away.
/// Runs with the tenant context already set to the new restaurant.</summary>
internal sealed class TenantStarterContent(AppDbContext db) : ITenantStarterContent
{
    public async Task AddAsync(Guid tenantId, CancellationToken ct)
    {
        var breakfast = MenuCategory.Create("Breakfast", "Served until 11:30", 0, new TimeOnly(7, 0), new TimeOnly(11, 30)).Value;
        var small = MenuCategory.Create("Small plates", "Made to share", 1).Value;
        var mains = MenuCategory.Create("Mains", "From the wood-fired oven", 2).Value;
        var desserts = MenuCategory.Create("Desserts", null, 3).Value;
        var drinks = MenuCategory.Create("Drinks", "Coffee, coolers and more", 4).Value;
        db.MenuCategories.AddRange(breakfast, small, mains, desserts, drinks);

        ModifierGroup Extras() => new()
        {
            Name = "Extras", MinSelect = 0, MaxSelect = 3,
            Options = [new() { Name = "Burrata", PriceDelta = 180 }, new() { Name = "Chilli oil", PriceDelta = 30 }, new() { Name = "Extra cheese", PriceDelta = 80 }]
        };
        var size = new ModifierGroup { Name = "Size", MinSelect = 1, MaxSelect = 1, Options = [new() { Name = "Regular" }, new() { Name = "Large", PriceDelta = 60 }] };
        var milk = new ModifierGroup { Name = "Milk", MinSelect = 0, MaxSelect = 1, Options = [new() { Name = "Oat milk", PriceDelta = 40 }, new() { Name = "Almond milk", PriceDelta = 40 }] };

        db.MenuItems.AddRange(
            MenuItem.Create(breakfast.Id, "Masala omelette", "Three eggs, green chilli, onion, sourdough toast", 280, 10, [DietaryTag.Spicy]).Value,
            MenuItem.Create(breakfast.Id, "Avocado sourdough", "Smashed avocado, feta, pickled onion, seeds", 360, 8, [DietaryTag.Vegetarian]).Value,
            MenuItem.Create(small.Id, "Charred corn ribs", "Smoked paprika butter, lime, cotija", 320, 12, [DietaryTag.Vegetarian, DietaryTag.GlutenFree]).Value,
            MenuItem.Create(small.Id, "Crispy calamari", "Lemon aioli, curry leaf salt", 420, 12, [DietaryTag.ChefsPick]).Value,
            MenuItem.Create(small.Id, "Burnt carrot hummus", "Dukkah, olive oil, warm pita", 340, 6, [DietaryTag.Vegan, DietaryTag.ContainsNuts]).Value,
            MenuItem.Create(mains.Id, "Margherita", "San Marzano tomato, fior di latte, basil", 520, 15, [DietaryTag.Vegetarian], modifierGroups: [Extras()]).Value,
            MenuItem.Create(mains.Id, "Nduja & hot honey", "Spicy nduja, mozzarella, hot honey drizzle", 640, 15, [DietaryTag.Spicy, DietaryTag.ChefsPick], modifierGroups: [Extras()]).Value,
            MenuItem.Create(mains.Id, "Butter chicken risotto", "Arborio, makhani sauce, charred chicken", 590, 20, [DietaryTag.New]).Value,
            MenuItem.Create(mains.Id, "Wild mushroom pappardelle", "Fresh pasta, truffle cream, parmesan", 560, 16, [DietaryTag.Vegetarian]).Value,
            MenuItem.Create(desserts.Id, "Basque cheesecake", "Burnt top, berry compote", 340, 4, [DietaryTag.Vegetarian]).Value,
            MenuItem.Create(desserts.Id, "Dark chocolate mousse", "Sea salt, olive oil, hazelnut crumb", 310, 4, [DietaryTag.Vegetarian, DietaryTag.ContainsNuts]).Value,
            MenuItem.Create(drinks.Id, "Flat white", "Double ristretto, steamed milk", 190, 4, null, modifierGroups: [size, milk]).Value,
            MenuItem.Create(drinks.Id, "Cold brew tonic", "18-hour cold brew, tonic, orange", 240, 3, [DietaryTag.Vegan]).Value,
            MenuItem.Create(drinks.Id, "Kokum cooler", "Kokum, mint, soda, black salt", 180, 3, [DietaryTag.Vegan, DietaryTag.GlutenFree]).Value);

        for (var i = 1; i <= 8; i++)
            db.Tables.Add(DiningTable.Create($"T{i}", $"Table {i}", i % 3 == 0 ? 6 : i % 2 == 0 ? 4 : 2, "Indoor", (i - 1) % 4, (i - 1) / 4).Value);
        for (var i = 1; i <= 4; i++)
            db.Tables.Add(DiningTable.Create($"P{i}", $"Patio {i}", 4, "Terrace", i - 1, 0).Value);
        for (var i = 1; i <= 3; i++)
            db.Tables.Add(DiningTable.Create($"B{i}", $"Bar {i}", 2, "Bar", i - 1, 0).Value);

        await db.SaveChangesAsync(ct);
    }
}
