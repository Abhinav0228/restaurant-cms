using RestaurantCms.Api.Caching;
using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Menu;

namespace RestaurantCms.Api.Features.Menu;

public sealed record AvailabilityRequest(bool IsAvailable);
public sealed record ReorderRequest(IReadOnlyList<Guid> OrderedIds);

public sealed class MenuEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        var read = routes.Staff.MapGroup("/menu").WithTags("Menu");
        var manage = routes.Staff.MapGroup("/menu").WithTags("Menu").RequireAuthorization(Policies.Manager);

        read.MapGet("/categories", async (IQueryHandler<GetCategories.Query, IReadOnlyList<CategoryDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetCategories.Query(), ct)).ToOk());

        manage.MapPost("/categories", async (CategoryFields body, ICommandHandler<CreateCategory.Command, CategoryDto> h, CancellationToken ct) =>
            (await h.Handle(new CreateCategory.Command(body), ct)).ToCreated(c => $"/api/v1/menu/categories/{c.Id}"));

        manage.MapPut("/categories/{id:guid}", async (Guid id, CategoryFields body, ICommandHandler<UpdateCategory.Command, CategoryDto> h, CancellationToken ct) =>
            (await h.Handle(new UpdateCategory.Command(id, body), ct)).ToOk());

        manage.MapDelete("/categories/{id:guid}", async (Guid id, ICommandHandler<DeleteCategory.Command, Unit> h, CancellationToken ct) =>
            (await h.Handle(new DeleteCategory.Command(id), ct)).ToNoContent());

        manage.MapPut("/categories/order", async (ReorderRequest body, ICommandHandler<ReorderCategories.Command, Unit> h, CancellationToken ct) =>
            (await h.Handle(new ReorderCategories.Command(body.OrderedIds), ct)).ToNoContent());

        read.MapGet("/items", async (Guid? categoryId, string? search, IQueryHandler<GetMenuItems.Query, IReadOnlyList<MenuItemDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetMenuItems.Query(categoryId, search), ct)).ToOk());

        read.MapGet("/items/{id:guid}", async (Guid id, IQueryHandler<GetMenuItem.Query, MenuItemDto> h, CancellationToken ct) =>
            (await h.Handle(new GetMenuItem.Query(id), ct)).ToOk());

        manage.MapPost("/items", async (MenuItemFields body, ICommandHandler<CreateMenuItem.Command, MenuItemDto> h, CancellationToken ct) =>
            (await h.Handle(new CreateMenuItem.Command(body), ct)).ToCreated(i => $"/api/v1/menu/items/{i.Id}"));

        manage.MapPut("/items/{id:guid}", async (Guid id, MenuItemFields body, ICommandHandler<UpdateMenuItem.Command, MenuItemDto> h, CancellationToken ct) =>
            (await h.Handle(new UpdateMenuItem.Command(id, body), ct)).ToOk());

        manage.MapDelete("/items/{id:guid}", async (Guid id, ICommandHandler<DeleteMenuItem.Command, Unit> h, CancellationToken ct) =>
            (await h.Handle(new DeleteMenuItem.Command(id), ct)).ToNoContent());

        // Sold-out toggle: any staff member can flip it during service.
        read.MapPatch("/items/{id:guid}/availability", async (Guid id, AvailabilityRequest body, ICommandHandler<SetMenuItemAvailability.Command, MenuItemDto> h, CancellationToken ct) =>
            (await h.Handle(new SetMenuItemAvailability.Command(id, body.IsAvailable), ct)).ToOk());

        // Guests
        routes.Public.MapGet("/menu", async (IQueryHandler<GetPublicMenu.Query, PublicMenuDto> h, CancellationToken ct) =>
            (await h.Handle(new GetPublicMenu.Query(), ct)).ToOk())
            .WithTags("Public").CacheOutput(PublicMenuCachePolicy.Name);
    }
}
