using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Dashboard;

namespace RestaurantCms.Api.Features.Dashboard;

public sealed class DashboardEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes) =>
        routes.Staff.MapGet("/dashboard", async (IQueryHandler<GetDashboard.Query, DashboardDto> h, CancellationToken ct) =>
                (await h.Handle(new GetDashboard.Query(), ct)).ToOk())
            .WithTags("Dashboard").RequireAuthorization(Policies.Floor);
}
