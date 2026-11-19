using RestaurantCms.Application.Features.Offers;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Menu;

namespace RestaurantCms.Application.Features.Menu;

public sealed record CategoryDto(Guid Id, string Name, string? Description, int SortOrder, bool IsActive,
    TimeOnly? AvailableFrom, TimeOnly? AvailableUntil, int ItemCount);

public sealed record ModifierOptionDto(Guid Id, string Name, decimal PriceDelta);

public sealed record ModifierGroupDto(Guid Id, string Name, int MinSelect, int MaxSelect, IReadOnlyList<ModifierOptionDto> Options);

public sealed record MenuItemDto(Guid Id, Guid CategoryId, string Name, string? Description, decimal Price,
    string? ImageUrl, IReadOnlyList<DietaryTag> Tags, int PrepMinutes, int? Calories, int SortOrder,
    bool IsActive, bool IsAvailable, IReadOnlyList<ModifierGroupDto> ModifierGroups);

public sealed record PublicMenuDto(string RestaurantName, string? Tagline, string Currency,
    IReadOnlyList<PublicCategoryDto> Categories, IReadOnlyList<OfferDto> LiveOffers,
    string? Address, string? Phone, string? OpeningHours, string Palette);

public sealed record PublicCategoryDto(Guid Id, string Name, string? Description, bool ServingNow,
    TimeOnly? AvailableFrom, TimeOnly? AvailableUntil, IReadOnlyList<MenuItemDto> Items);

public static class MenuMapping
{
    public static CategoryDto ToDto(this MenuCategory c, int itemCount) =>
        new(c.Id, c.Name, c.Description, c.SortOrder, c.IsActive, c.AvailableFrom, c.AvailableUntil, itemCount);

    public static MenuItemDto ToDto(this MenuItem i) =>
        new(i.Id, i.CategoryId, i.Name, i.Description, i.Price, i.ImageUrl, i.Tags, i.PrepMinutes, i.Calories,
            i.SortOrder, i.IsActive, i.IsAvailable,
            i.ModifierGroups.Select(g => new ModifierGroupDto(g.Id, g.Name, g.MinSelect, g.MaxSelect,
                g.Options.Select(o => new ModifierOptionDto(o.Id, o.Name, o.PriceDelta)).ToList())).ToList());

    public static ModifierGroup ToDomain(this ModifierGroupDto g) => new()
    {
        Id = g.Id == Guid.Empty ? Guid.NewGuid() : g.Id,
        Name = g.Name?.Trim() ?? string.Empty,
        MinSelect = g.MinSelect,
        MaxSelect = g.MaxSelect,
        Options = (g.Options ?? []).Select(o => new ModifierOption
        {
            Id = o.Id == Guid.Empty ? Guid.NewGuid() : o.Id,
            Name = o.Name?.Trim() ?? string.Empty,
            PriceDelta = o.PriceDelta
        }).ToList()
    };
}
