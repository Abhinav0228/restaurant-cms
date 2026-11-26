using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Settings;

namespace RestaurantCms.Api.Features.Settings;

public sealed class SettingsEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        var group = routes.Staff.MapGroup("/settings").WithTags("Settings");
        group.MapGet("", async (IQueryHandler<GetSettings.Query, SettingsDto> h, CancellationToken ct) =>
            (await h.Handle(new GetSettings.Query(), ct)).ToOk());
        group.MapPut("", async (SettingsDto body, ICommandHandler<UpdateSettings.Command, SettingsDto> h, CancellationToken ct) =>
            (await h.Handle(new UpdateSettings.Command(body), ct)).ToOk())
            .RequireAuthorization(Policies.Admin);
    }
}
