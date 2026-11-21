using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Menu;

namespace RestaurantCms.Application.Features.Menu;

public sealed record MenuItemFields(Guid CategoryId, string Name, string? Description, decimal Price, int PrepMinutes,
    IReadOnlyList<DietaryTag>? Tags, string? ImageUrl, int? Calories, IReadOnlyList<ModifierGroupDto>? ModifierGroups,
    bool IsActive = true);

internal sealed class MenuItemFieldsRules : Validator<MenuItemFields>
{
    public MenuItemFieldsRules()
    {
        NotEmpty("categoryId", x => x.CategoryId);
        Required("name", x => x.Name);
        MaxLength("name", x => x.Name, 120);
        MaxLength("description", x => x.Description, 600);
        Range("price", x => x.Price, 0, 1_000_000);
        Range("prepMinutes", x => x.PrepMinutes, 0, 240);
        Rule("imageUrl", x => string.IsNullOrWhiteSpace(x.ImageUrl) || Uri.TryCreate(x.ImageUrl, UriKind.Absolute, out _),
            "Photo URL must be a full link, e.g. https://…");
    }
}

public static class GetMenuItems
{
    public sealed record Query(Guid? CategoryId = null, string? Search = null) : IQuery<IReadOnlyList<MenuItemDto>>;

    internal sealed class Handler(IMenuRepository menu) : IQueryHandler<Query, IReadOnlyList<MenuItemDto>>
    {
        public async Task<Result<IReadOnlyList<MenuItemDto>>> Handle(Query query, CancellationToken ct) =>
            (await menu.GetItemsAsync(query.CategoryId, query.Search, ct))
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Name).Select(i => i.ToDto()).ToList();
    }
}

public static class GetMenuItem
{
    public sealed record Query(Guid Id) : IQuery<MenuItemDto>;

    internal sealed class Handler(IMenuRepository menu) : IQueryHandler<Query, MenuItemDto>
    {
        public async Task<Result<MenuItemDto>> Handle(Query query, CancellationToken ct) =>
            await menu.GetItemAsync(query.Id, ct) is { } item ? item.ToDto() : Error.NotFound("MenuItem", query.Id);
    }
}

public static class CreateMenuItem
{
    public sealed record Command(MenuItemFields Fields) : ICommand<MenuItemDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new MenuItemFieldsRules());
    }

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow) : ICommandHandler<Command, MenuItemDto>
    {
        public async Task<Result<MenuItemDto>> Handle(Command command, CancellationToken ct)
        {
            var f = command.Fields;
            if (await menu.GetCategoryAsync(f.CategoryId, ct) is null)
                return Error.Validation("MenuItem.Category", "The selected section does not exist.");

            var siblings = await menu.GetItemsAsync(f.CategoryId, ct: ct);
            var created = MenuItem.Create(f.CategoryId, f.Name, f.Description, f.Price, f.PrepMinutes, f.Tags, f.ImageUrl,
                f.Calories, f.ModifierGroups?.Select(g => g.ToDomain()), siblings.Count == 0 ? 0 : siblings.Max(s => s.SortOrder) + 1);
            if (created.IsFailure) return created.Error!;
            var item = created.Value;
            if (!f.IsActive)
                item.Update(item.CategoryId, item.Name, item.Description, item.Price, item.PrepMinutes, item.Tags, item.ImageUrl,
                    item.Calories, item.ModifierGroups, false);

            menu.AddItem(item);
            await uow.SaveChangesAsync(ct);
            return item.ToDto();
        }
    }
}

public static class UpdateMenuItem
{
    public sealed record Command(Guid Id, MenuItemFields Fields) : ICommand<MenuItemDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new MenuItemFieldsRules());
    }

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, MenuItemDto>
    {
        public async Task<Result<MenuItemDto>> Handle(Command command, CancellationToken ct)
        {
            var item = await menu.GetItemAsync(command.Id, ct);
            if (item is null) return Error.NotFound("MenuItem", command.Id);
            var f = command.Fields;
            if (await menu.GetCategoryAsync(f.CategoryId, ct) is null)
                return Error.Validation("MenuItem.Category", "The selected section does not exist.");

            var result = item.Update(f.CategoryId, f.Name, f.Description, f.Price, f.PrepMinutes, f.Tags, f.ImageUrl,
                f.Calories, f.ModifierGroups?.Select(g => g.ToDomain()), f.IsActive);
            if (result.IsFailure) return result.Error!;
            item.Touch(clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return item.ToDto();
        }
    }
}

public static class SetMenuItemAvailability
{
    public sealed record Command(Guid Id, bool IsAvailable) : ICommand<MenuItemDto>;

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, MenuItemDto>
    {
        public async Task<Result<MenuItemDto>> Handle(Command command, CancellationToken ct)
        {
            var item = await menu.GetItemAsync(command.Id, ct);
            if (item is null) return Error.NotFound("MenuItem", command.Id);
            item.SetAvailability(command.IsAvailable);
            item.Touch(clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return item.ToDto();
        }
    }
}

public static class DeleteMenuItem
{
    public sealed record Command(Guid Id) : ICommand<Unit>;

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow) : ICommandHandler<Command, Unit>
    {
        public async Task<Result<Unit>> Handle(Command command, CancellationToken ct)
        {
            var item = await menu.GetItemAsync(command.Id, ct);
            if (item is null) return Error.NotFound("MenuItem", command.Id);
            menu.RemoveItem(item);
            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
