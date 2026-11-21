using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Offers;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Features.Menu;

/// <summary>The guest-facing menu of the current restaurant: active sections/items only,
/// "serving now" flags, live offers and branding. Cached per tenant by the API.</summary>
public static class GetPublicMenu
{
    public sealed record Query : IQuery<PublicMenuDto>;

    internal sealed class Handler(IMenuRepository menu, IOfferRepository offers, ISettingsRepository settings, IClock clock)
        : IQueryHandler<Query, PublicMenuDto>
    {
        public async Task<Result<PublicMenuDto>> Handle(Query query, CancellationToken ct)
        {
            var s = await settings.GetAsync(ct);
            var now = clock.UtcNow;
            var localTime = s.LocalTime(now);
            var categories = (await menu.GetCategoriesAsync(ct)).Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToList();
            var items = (await menu.GetItemsAsync(ct: ct)).Where(i => i.IsActive).ToList();
            var live = (await offers.GetAllAsync(ct)).Where(o => o.IsEnabled && now >= o.StartsAt && now < o.EndsAt)
                .Select(o => o.ToDto() with { IsLive = true }).ToList();

            var publicCategories = categories
                .Select(c => new PublicCategoryDto(c.Id, c.Name, c.Description, c.IsServingAt(localTime), c.AvailableFrom,
                    c.AvailableUntil,
                    items.Where(i => i.CategoryId == c.Id).OrderBy(i => i.SortOrder).ThenBy(i => i.Name).Select(i => i.ToDto()).ToList()))
                .Where(c => c.Items.Count > 0)
                .ToList();

            return new PublicMenuDto(s.Name, s.Tagline, s.Currency, publicCategories, live, s.Address, s.Phone, s.OpeningHours, s.Palette);
        }
    }
}
