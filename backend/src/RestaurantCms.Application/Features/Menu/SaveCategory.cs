using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Menu;

namespace RestaurantCms.Application.Features.Menu;

/// <summary>Fields shared by create and update.</summary>
public sealed record CategoryFields(string Name, string? Description, bool IsActive = true,
    TimeOnly? AvailableFrom = null, TimeOnly? AvailableUntil = null);

internal sealed class CategoryFieldsRules : Validator<CategoryFields>
{
    public CategoryFieldsRules()
    {
        Required("name", x => x.Name);
        MaxLength("name", x => x.Name, 80);
        MaxLength("description", x => x.Description, 400);
        Rule("availableUntil", x => x.AvailableFrom.HasValue == x.AvailableUntil.HasValue,
            "Set both serving times, or neither.");
    }
}

public static class CreateCategory
{
    public sealed record Command(CategoryFields Fields) : ICommand<CategoryDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new CategoryFieldsRules());
    }

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow) : ICommandHandler<Command, CategoryDto>
    {
        public async Task<Result<CategoryDto>> Handle(Command command, CancellationToken ct)
        {
            var f = command.Fields;
            var existing = await menu.GetCategoriesAsync(ct);
            if (existing.Any(c => string.Equals(c.Name, f.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Error.Conflict("Category.Duplicate", $"A section named '{f.Name.Trim()}' already exists.");

            var nextSort = existing.Count == 0 ? 0 : existing.Max(c => c.SortOrder) + 1;
            var created = MenuCategory.Create(f.Name, f.Description, nextSort, f.AvailableFrom, f.AvailableUntil);
            if (created.IsFailure) return created.Error!;
            if (!f.IsActive) created.Value.Update(f.Name, f.Description, false, f.AvailableFrom, f.AvailableUntil);

            menu.AddCategory(created.Value);
            await uow.SaveChangesAsync(ct);
            return created.Value.ToDto(0);
        }
    }
}

public static class UpdateCategory
{
    public sealed record Command(Guid Id, CategoryFields Fields) : ICommand<CategoryDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator() => Include(x => x.Fields, new CategoryFieldsRules());
    }

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, CategoryDto>
    {
        public async Task<Result<CategoryDto>> Handle(Command command, CancellationToken ct)
        {
            var category = await menu.GetCategoryAsync(command.Id, ct);
            if (category is null) return Error.NotFound("Category", command.Id);

            var f = command.Fields;
            var all = await menu.GetCategoriesAsync(ct);
            if (all.Any(c => c.Id != command.Id && string.Equals(c.Name, f.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Error.Conflict("Category.Duplicate", $"A section named '{f.Name}' already exists.");

            var result = category.Update(f.Name ?? "", f.Description, f.IsActive, f.AvailableFrom, f.AvailableUntil);
            if (result.IsFailure) return result.Error!;
            category.Touch(clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return category.ToDto((await menu.GetItemsAsync(command.Id, ct: ct)).Count);
        }
    }
}

public static class DeleteCategory
{
    public sealed record Command(Guid Id) : ICommand<Unit>;

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow) : ICommandHandler<Command, Unit>
    {
        public async Task<Result<Unit>> Handle(Command command, CancellationToken ct)
        {
            var category = await menu.GetCategoryAsync(command.Id, ct);
            if (category is null) return Error.NotFound("Category", command.Id);
            if (await menu.CategoryHasItemsAsync(command.Id, ct))
                return Error.Conflict("Category.HasItems", "Move or delete this section's items first.");
            menu.RemoveCategory(category);
            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}

public static class ReorderCategories
{
    public sealed record Command(IReadOnlyList<Guid> OrderedIds) : ICommand<Unit>;

    internal sealed class Handler(IMenuRepository menu, IUnitOfWork uow) : ICommandHandler<Command, Unit>
    {
        public async Task<Result<Unit>> Handle(Command command, CancellationToken ct)
        {
            var categories = await menu.GetCategoriesAsync(ct);
            var byId = categories.ToDictionary(c => c.Id);
            var ids = command.OrderedIds ?? [];
            if (ids.Count != categories.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => !byId.ContainsKey(id)))
                return Error.Validation("Category.Reorder", "The list must contain every section exactly once.");
            for (var i = 0; i < ids.Count; i++) byId[ids[i]].SetSortOrder(i);
            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
