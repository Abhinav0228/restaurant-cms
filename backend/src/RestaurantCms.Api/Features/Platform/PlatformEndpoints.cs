using RestaurantCms.Api.Endpoints;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Features.Platform;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Api.Features.Platform;

public sealed record SubscriptionRequest(TenantPlan Plan, TenantStatus Status);

/// <summary>Operator API for the SaaS: onboard restaurants, change plans, suspend accounts.</summary>
public sealed class PlatformEndpoints : IEndpointModule
{
    public void Map(ApiRoutes routes)
    {
        var tenants = routes.Platform.MapGroup("/tenants").WithTags("Platform");
        tenants.MapGet("", async (IQueryHandler<GetTenants.Query, IReadOnlyList<TenantDto>> h, CancellationToken ct) =>
            (await h.Handle(new GetTenants.Query(), ct)).ToOk());
        tenants.MapPost("", async (CreateTenant.Command body, ICommandHandler<CreateTenant.Command, TenantDto> h, CancellationToken ct) =>
            (await h.Handle(body, ct)).ToCreated(t => $"/api/v1/platform/tenants/{t.Id}"));
        tenants.MapPut("/{id:guid}/subscription", async (Guid id, SubscriptionRequest body, ICommandHandler<UpdateTenantSubscription.Command, TenantDto> h, CancellationToken ct) =>
            (await h.Handle(new UpdateTenantSubscription.Command(id, body.Plan, body.Status), ct)).ToOk());
    }
}
