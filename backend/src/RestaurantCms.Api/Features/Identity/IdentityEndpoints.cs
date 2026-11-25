using RestaurantCms.Api.Endpoints;
using RestaurantCms.Api.Security;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Identity;

namespace RestaurantCms.Api.Features.Identity;

public sealed class IdentityEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        routes.Anonymous.MapPost("/auth/login", async (Login.Command body, ICommandHandler<Login.Command, LoginResponse> h, CancellationToken ct) =>
                (await h.Handle(body, ct)).ToOk())
            .WithTags("Auth").RequireRateLimiting(Policies.PublicRateLimit);

        var staff = routes.Staff.MapGroup("/staff").WithTags("Staff").RequireAuthorization(Policies.Admin);
        staff.MapGet("", async (IQueryHandler<GetStaff.Query, IReadOnlyList<StaffDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetStaff.Query(), ct)).ToOk());
        staff.MapPost("", async (CreateStaff.Command body, ICommandHandler<CreateStaff.Command, StaffDto> h, CancellationToken ct) =>
            (await h.Handle(body, ct)).ToCreated(u => $"/api/v1/staff/{u.Id}"));
    }
}
